CREATE TABLE dbo.BankAccounts(
 AccountId uniqueidentifier NOT NULL PRIMARY KEY,Name nvarchar(100) NOT NULL,Iban nvarchar(34) NOT NULL UNIQUE,
 Currency nvarchar(3) NOT NULL DEFAULT N'TRY' CHECK(Currency=N'TRY'),Reason nvarchar(500) NOT NULL,
 CreatedByAccountId uniqueidentifier NOT NULL,CreatedBy nvarchar(50) NOT NULL,CreatedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),PayloadHash binary(32) NOT NULL
);
CREATE TABLE dbo.BankImportBatches(
 BatchId uniqueidentifier NOT NULL PRIMARY KEY,AccountId uniqueidentifier NOT NULL REFERENCES dbo.BankAccounts(AccountId),
 ImportHash binary(32) NOT NULL,FileHash binary(32) NOT NULL,FileName nvarchar(150) NOT NULL,Mapping nvarchar(2000) NOT NULL,
 AddedCount int NOT NULL CHECK(AddedCount BETWEEN 0 AND 200),DuplicateCount int NOT NULL CHECK(DuplicateCount BETWEEN 0 AND 200),
 Reason nvarchar(500) NOT NULL,CreatedByAccountId uniqueidentifier NOT NULL,CreatedBy nvarchar(50) NOT NULL,CreatedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),PayloadHash binary(32) NOT NULL,
 CONSTRAINT UX_BankImportHash UNIQUE(AccountId,ImportHash),CONSTRAINT UX_BankBatchScope UNIQUE(AccountId,BatchId)
);
CREATE TABLE dbo.BankTransactions(
 TransactionId uniqueidentifier NOT NULL PRIMARY KEY,AccountId uniqueidentifier NOT NULL REFERENCES dbo.BankAccounts(AccountId),
 BatchId uniqueidentifier NOT NULL,SourceLine int NOT NULL CHECK(SourceLine BETWEEN 2 AND 201),
 ExternalId nvarchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL,BookingDay date NOT NULL,
 SignedAmount decimal(18,2) NOT NULL CHECK(SignedAmount<>0),Currency nvarchar(3) NOT NULL CHECK(Currency=N'TRY'),
 CounterpartyIban nvarchar(34) NOT NULL,Description nvarchar(500) NOT NULL,PaymentReference uniqueidentifier NULL,RowHash binary(32) NOT NULL,
 CONSTRAINT FK_BankTransactionBatch FOREIGN KEY(AccountId,BatchId) REFERENCES dbo.BankImportBatches(AccountId,BatchId),
 CONSTRAINT UX_BankTransactionSource UNIQUE(AccountId,ExternalId),CONSTRAINT UX_BankTransactionLine UNIQUE(BatchId,SourceLine)
);
CREATE INDEX IX_BankTransactionDay ON dbo.BankTransactions(AccountId,BookingDay DESC,TransactionId) INCLUDE(SignedAmount,PaymentReference);
CREATE TABLE dbo.BankPaymentMatches(
 MatchId uniqueidentifier NOT NULL PRIMARY KEY,TransactionId uniqueidentifier NOT NULL REFERENCES dbo.BankTransactions(TransactionId),
 LedgerId bigint NOT NULL REFERENCES dbo.PaymentLedger(LedgerId),Reason nvarchar(500) NOT NULL,CreatedByAccountId uniqueidentifier NOT NULL,CreatedBy nvarchar(50) NOT NULL,
 CreatedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),PayloadHash binary(32) NOT NULL,
 ReleaseId uniqueidentifier NULL,ReleasedUtc datetime2(7) NULL,ReleasedByAccountId uniqueidentifier NULL,ReleasedBy nvarchar(50) NULL,ReleaseReason nvarchar(500) NULL,ReleaseHash binary(32) NULL,
 RecordVersion rowversion NOT NULL,
 CONSTRAINT CK_BankMatchRelease CHECK((ReleaseId IS NULL AND ReleasedUtc IS NULL AND ReleasedByAccountId IS NULL AND ReleasedBy IS NULL AND ReleaseReason IS NULL AND ReleaseHash IS NULL)
 OR(ReleaseId IS NOT NULL AND ReleasedUtc IS NOT NULL AND ReleasedByAccountId IS NOT NULL AND ReleasedBy IS NOT NULL AND ReleaseReason IS NOT NULL AND ReleaseHash IS NOT NULL))
);
CREATE UNIQUE INDEX UX_BankActiveTransaction ON dbo.BankPaymentMatches(TransactionId) WHERE ReleasedUtc IS NULL;
CREATE UNIQUE INDEX UX_BankActivePayment ON dbo.BankPaymentMatches(LedgerId) WHERE ReleasedUtc IS NULL;
CREATE UNIQUE INDEX UX_BankReleaseCommand ON dbo.BankPaymentMatches(ReleaseId) WHERE ReleaseId IS NOT NULL;
CREATE INDEX IX_BankMatchHistory ON dbo.BankPaymentMatches(TransactionId,CreatedUtc DESC,MatchId);
CREATE INDEX IX_BankMatchLedgerHistory ON dbo.BankPaymentMatches(LedgerId,CreatedUtc DESC,MatchId) INCLUDE(TransactionId,RecordVersion);
CREATE INDEX IX_BankImportChronology ON dbo.BankImportBatches(AccountId,CreatedUtc DESC,BatchId);
CREATE INDEX IX_BankPaymentCandidates ON dbo.PaymentLedger(Delta,CreatedUtc,LedgerId)
 INCLUDE(RequestId,PersonId,TC,PersonnelGroup,ProjectId,ReversedUtc)
 WHERE Kind=N'Payment' AND Delta<0 AND RequestId IS NOT NULL AND PersonId IS NOT NULL;
GO
CREATE TRIGGER dbo.GuardBankAccount ON dbo.BankAccounts AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;THROW 51300,N'Banka hesap kimliği ve geçmişi değişmez.',1;END;
GO
CREATE TRIGGER dbo.GuardBankImport ON dbo.BankImportBatches AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;THROW 51301,N'Banka aktarım geçmişi değişmez.',1;END;
GO
CREATE TRIGGER dbo.GuardBankTransaction ON dbo.BankTransactions AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;THROW 51302,N'Ham banka hareketleri değişmez.',1;END;
GO
CREATE TRIGGER dbo.GuardBankPaymentMatch ON dbo.BankPaymentMatches AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.MatchId=D.MatchId WHERE I.MatchId IS NULL OR D.ReleasedUtc IS NOT NULL OR I.ReleasedUtc IS NULL)
 THROW 51303,N'Eşleştirme yalnız gerekçeli olarak kaldırılabilir; geçmiş değişmez.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.MatchId=I.MatchId WHERE EXISTS(
 SELECT I.TransactionId,I.LedgerId,I.Reason,I.CreatedByAccountId,I.CreatedBy,I.CreatedUtc,I.PayloadHash EXCEPT
 SELECT D.TransactionId,D.LedgerId,D.Reason,D.CreatedByAccountId,D.CreatedBy,D.CreatedUtc,D.PayloadHash))
 THROW 51304,N'İlk eşleştirme değerleri değiştirilemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I LEFT JOIN deleted D ON D.MatchId=I.MatchId WHERE D.MatchId IS NULL AND (I.ReleaseId IS NOT NULL OR NOT EXISTS(
 SELECT 1 FROM dbo.BankTransactions B WITH(UPDLOCK,HOLDLOCK) JOIN dbo.PaymentLedger P WITH(UPDLOCK,HOLDLOCK) ON P.LedgerId=I.LedgerId
 WHERE B.TransactionId=I.TransactionId AND B.SignedAmount<0 AND -B.SignedAmount=-P.Delta AND B.Currency=N'TRY'
 AND P.Kind=N'Payment' AND P.Delta<0 AND P.RequestId IS NOT NULL AND P.PersonId IS NOT NULL AND P.ReversedUtc IS NULL
 AND(B.PaymentReference IS NULL OR B.PaymentReference=P.RequestId))))
 THROW 51305,N'Eşleştirme geçerli, tam tutarlı banka çıkışı ve ödeme ile yapılmalı.',1;
END;
GO
CREATE TRIGGER dbo.GuardBankMatchedPayment ON dbo.PaymentLedger AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D JOIN dbo.BankPaymentMatches M ON M.LedgerId=D.LedgerId AND M.ReleasedUtc IS NULL)
 THROW 51306,N'Ödemeyi iptal etmeden önce banka eşleştirmesini gerekçeyle kaldırın.',1;
END;
GO
UPDATE dbo.admin SET SessionEpoch=NEWID();

GO
CREATE TRIGGER dbo.Audit_BankAccounts ON dbo.BankAccounts AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'Bank.Change',N'BankAccounts',CONVERT(nvarchar(100),COALESCE(I.AccountId,D.AccountId)),CASE WHEN D.AccountId IS NULL THEN N'Insert' WHEN I.AccountId IS NULL THEN N'Delete' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',CASE WHEN D.AccountId IS NULL THEN NULL ELSE(SELECT D.[AccountId],D.[Name],D.[Currency],D.[Reason] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 CASE WHEN I.AccountId IS NULL THEN NULL ELSE(SELECT I.[AccountId],I.[Name],I.[Currency],I.[Reason] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application'
 FROM inserted I FULL OUTER JOIN deleted D ON D.AccountId=I.AccountId;
END;

GO
CREATE TRIGGER dbo.Audit_BankImportBatches ON dbo.BankImportBatches AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'Bank.Change',N'BankImportBatches',CONVERT(nvarchar(100),COALESCE(I.BatchId,D.BatchId)),CASE WHEN D.BatchId IS NULL THEN N'Insert' WHEN I.BatchId IS NULL THEN N'Delete' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',CASE WHEN D.BatchId IS NULL THEN NULL ELSE(SELECT D.[BatchId],D.[AccountId],D.[FileHash],D.[ImportHash],D.[AddedCount],D.[DuplicateCount],D.[Reason] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 CASE WHEN I.BatchId IS NULL THEN NULL ELSE(SELECT I.[BatchId],I.[AccountId],I.[FileHash],I.[ImportHash],I.[AddedCount],I.[DuplicateCount],I.[Reason] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application'
 FROM inserted I FULL OUTER JOIN deleted D ON D.BatchId=I.BatchId;
END;

GO
CREATE TRIGGER dbo.Audit_BankTransactions ON dbo.BankTransactions AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'Bank.Change',N'BankTransactions',CONVERT(nvarchar(100),COALESCE(I.TransactionId,D.TransactionId)),CASE WHEN D.TransactionId IS NULL THEN N'Insert' WHEN I.TransactionId IS NULL THEN N'Delete' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',CASE WHEN D.TransactionId IS NULL THEN NULL ELSE(SELECT D.[TransactionId],D.[AccountId],D.[BatchId],D.[SourceLine],D.[ExternalId],D.[BookingDay],D.[SignedAmount],D.[Currency],D.[PaymentReference],D.[RowHash] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 CASE WHEN I.TransactionId IS NULL THEN NULL ELSE(SELECT I.[TransactionId],I.[AccountId],I.[BatchId],I.[SourceLine],I.[ExternalId],I.[BookingDay],I.[SignedAmount],I.[Currency],I.[PaymentReference],I.[RowHash] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application'
 FROM inserted I FULL OUTER JOIN deleted D ON D.TransactionId=I.TransactionId;
END;

GO
CREATE TRIGGER dbo.Audit_BankPaymentMatches ON dbo.BankPaymentMatches AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'Bank.Change',N'BankPaymentMatches',CONVERT(nvarchar(100),COALESCE(I.MatchId,D.MatchId)),CASE WHEN D.MatchId IS NULL THEN N'Insert' WHEN I.MatchId IS NULL THEN N'Delete' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',CASE WHEN D.MatchId IS NULL THEN NULL ELSE(SELECT D.[MatchId],D.[TransactionId],D.[LedgerId],D.[Reason],D.[CreatedByAccountId],D.[CreatedBy],D.[CreatedUtc],D.[ReleaseId],D.[ReleasedUtc],D.[ReleasedByAccountId],D.[ReleasedBy],D.[ReleaseReason] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 CASE WHEN I.MatchId IS NULL THEN NULL ELSE(SELECT I.[MatchId],I.[TransactionId],I.[LedgerId],I.[Reason],I.[CreatedByAccountId],I.[CreatedBy],I.[CreatedUtc],I.[ReleaseId],I.[ReleasedUtc],I.[ReleasedByAccountId],I.[ReleasedBy],I.[ReleaseReason] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application'
 FROM inserted I FULL OUTER JOIN deleted D ON D.MatchId=I.MatchId;
END;
