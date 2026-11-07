CREATE TABLE dbo.AccrualCorrections(
 CorrectionId uniqueidentifier NOT NULL PRIMARY KEY,SourceItemId uniqueidentifier NOT NULL REFERENCES dbo.AccrualPostingItems(ItemId),
 PreviousCorrectionId uniqueidentifier NULL REFERENCES dbo.AccrualCorrections(CorrectionId),
 OldAmount decimal(18,2) NOT NULL,NewAmount decimal(18,2) NOT NULL,Delta AS (NewAmount-OldAmount) PERSISTED,
 Reason nvarchar(500) NOT NULL,CreatedByAccountId uniqueidentifier NOT NULL,CreatedBy nvarchar(50) NOT NULL,CreatedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
 PayloadHash binary(32) NOT NULL,RecordVersion rowversion NOT NULL,
 CONSTRAINT CK_CorrectionAmounts CHECK(OldAmount>=0 AND NewAmount>=0 AND OldAmount<>NewAmount),
 CONSTRAINT CK_CorrectionPrevious CHECK(PreviousCorrectionId IS NULL OR PreviousCorrectionId<>CorrectionId)
);
CREATE TABLE dbo.AccrualCorrectionApprovals(
 ApprovalId uniqueidentifier NOT NULL PRIMARY KEY,CorrectionId uniqueidentifier NOT NULL UNIQUE REFERENCES dbo.AccrualCorrections(CorrectionId),
 ApprovedByAccountId uniqueidentifier NOT NULL,ApprovedBy nvarchar(50) NOT NULL,ApprovedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),PayloadHash binary(32) NOT NULL
);
ALTER TABLE dbo.PaymentLedger ADD AccrualCorrectionId uniqueidentifier NULL REFERENCES dbo.AccrualCorrections(CorrectionId);
GO
CREATE UNIQUE INDEX UX_LedgerAccrualCorrection ON dbo.PaymentLedger(AccrualCorrectionId) WHERE AccrualCorrectionId IS NOT NULL;
CREATE TABLE dbo.AccrualCorrectionTransfers(
 TransferId uniqueidentifier NOT NULL PRIMARY KEY,SourceItemId uniqueidentifier NOT NULL REFERENCES dbo.AccrualPostingItems(ItemId),PreviousCorrectionId uniqueidentifier NULL REFERENCES dbo.AccrualCorrections(CorrectionId),[Sequence] bigint NOT NULL IDENTITY UNIQUE,CorrectionId uniqueidentifier NOT NULL UNIQUE REFERENCES dbo.AccrualCorrections(CorrectionId),
 ApprovalId uniqueidentifier NOT NULL REFERENCES dbo.AccrualCorrectionApprovals(ApprovalId),LedgerId bigint NOT NULL UNIQUE REFERENCES dbo.PaymentLedger(LedgerId),
 BeforeBalance decimal(18,2) NOT NULL,AfterBalance decimal(18,2) NOT NULL,PostedByAccountId uniqueidentifier NOT NULL,PostedBy nvarchar(50) NOT NULL,
 PostedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),PayloadHash binary(32) NOT NULL
);
CREATE UNIQUE INDEX UX_CorrectionFirstTransfer ON dbo.AccrualCorrectionTransfers(SourceItemId) WHERE PreviousCorrectionId IS NULL;
CREATE UNIQUE INDEX UX_CorrectionNextTransfer ON dbo.AccrualCorrectionTransfers(PreviousCorrectionId) WHERE PreviousCorrectionId IS NOT NULL;
CREATE INDEX IX_CorrectionSource ON dbo.AccrualCorrections(SourceItemId);
GO
CREATE VIEW dbo.AccrualItemBalances AS
 SELECT I.*,COALESCE(X.NewAmount,I.Amount) EffectiveAmount,X.CorrectionId LastCorrectionId
 FROM dbo.AccrualPostingItems I OUTER APPLY(SELECT TOP(1) R.NewAmount,R.CorrectionId FROM dbo.AccrualCorrections R JOIN dbo.AccrualCorrectionTransfers T ON T.CorrectionId=R.CorrectionId WHERE R.SourceItemId=I.ItemId ORDER BY T.[Sequence] DESC) X;
GO
CREATE VIEW dbo.FinanceEffectiveEntries AS
 SELECT F.*,COALESCE(CONVERT(decimal(18,2),X.Delta),CONVERT(decimal(18,2),0)) AdjustmentAmount,CONVERT(decimal(18,2),F.Amount+COALESCE(CONVERT(decimal(18,2),X.Delta),0)) EffectiveAmount
 FROM dbo.FinanceEntries F OUTER APPLY(SELECT SUM(R.Delta) Delta FROM dbo.AccrualCorrections R JOIN dbo.AccrualPostingItems I ON I.ItemId=R.SourceItemId JOIN dbo.AccrualCorrectionTransfers T ON T.CorrectionId=R.CorrectionId WHERE I.FinanceEntryId=F.EntryId) X;
GO
CREATE TRIGGER dbo.GuardAccrualCorrection ON dbo.AccrualCorrections AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51270,N'Mali düzeltme gövdesi değiştirilemez veya silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted R WHERE NOT EXISTS(SELECT 1 FROM dbo.AccrualItemBalances I JOIN dbo.AccrualPostings P ON P.PostingId=I.PostingId AND P.IsSealed=1 WHERE I.ItemId=R.SourceItemId AND I.FinanceEntryId IS NOT NULL AND I.EffectiveAmount=R.OldAmount AND NOT EXISTS(SELECT I.LastCorrectionId EXCEPT SELECT R.PreviousCorrectionId))) THROW 51271,N'Düzeltme seçimi güncel aktarılmış mali satıra ait değil.',1;
END;
GO
CREATE TRIGGER dbo.GuardAccrualCorrectionApproval ON dbo.AccrualCorrectionApprovals AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51272,N'Düzeltme mali onayı değişmez.',1;
 IF EXISTS(SELECT 1 FROM inserted A JOIN dbo.AccrualCorrections R ON R.CorrectionId=A.CorrectionId JOIN dbo.AccrualPostingItems I ON I.ItemId=R.SourceItemId JOIN dbo.AccrualPostings P ON P.PostingId=I.PostingId WHERE dbo.SameAccountPerson(A.ApprovedByAccountId,R.CreatedByAccountId)=1 OR dbo.AccountRepresentsPerson(A.ApprovedByAccountId,I.PersonId)=1 OR EXISTS(SELECT 1 FROM dbo.AccrualCalculations C WHERE C.CalculationId=P.CalculationId AND dbo.SameAccountPerson(A.ApprovedByAccountId,C.CreatedByAccountId)=1)) THROW 51273,N'Düzeltme hazırlayan, asıl hesap hazırlayan veya hak sahibi kendi düzeltmesini onaylayamaz.',1;
END;
GO
CREATE TRIGGER dbo.GuardCorrectionLedger ON dbo.PaymentLedger AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted WHERE AccrualCorrectionId IS NOT NULL) OR EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.LedgerId=I.LedgerId WHERE I.AccrualCorrectionId IS NOT NULL) THROW 51274,N'Düzeltme defter satırı değişmez.',1;
 IF EXISTS(SELECT 1 FROM inserted G WHERE G.AccrualCorrectionId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.AccrualCorrections R JOIN dbo.AccrualCorrectionApprovals A ON A.CorrectionId=R.CorrectionId JOIN dbo.AccrualPostingItems I ON I.ItemId=R.SourceItemId JOIN dbo.AccrualPostings P ON P.PostingId=I.PostingId JOIN dbo.AccrualCalculations C ON C.CalculationId=P.CalculationId WHERE R.CorrectionId=G.AccrualCorrectionId AND G.PersonId=I.PersonId AND G.PersonnelGroup=I.PersonnelGroup AND G.FinanceEntryId=I.FinanceEntryId AND G.ProjectId=C.ProjectId AND G.Kind=N'Allocation' AND G.TC=(SELECT TC FROM dbo.PersonnelIdentities WHERE PersonId=I.PersonId) AND G.Delta=R.Delta AND G.AccrualPostingId IS NULL AND G.ReversedUtc IS NULL AND G.RequestId IS NULL AND NOT EXISTS(SELECT 1 FROM dbo.AccrualCorrectionTransfers T WHERE T.CorrectionId=R.CorrectionId))) THROW 51275,N'Defter satırı onaylı mali düzeltmeye ait olmalı.',1;
END;
GO
CREATE TRIGGER dbo.GuardAccrualCorrectionTransfer ON dbo.AccrualCorrectionTransfers AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51276,N'Mali düzeltme aktarımı değişmez.',1;
 IF EXISTS(SELECT 1 FROM inserted T JOIN dbo.AccrualCorrections R ON R.CorrectionId=T.CorrectionId JOIN dbo.AccrualPostingItems I ON I.ItemId=R.SourceItemId JOIN dbo.AccrualPostings P ON P.PostingId=I.PostingId JOIN dbo.AccrualCalculations C ON C.CalculationId=P.CalculationId JOIN dbo.AccrualCorrectionApprovals A ON A.ApprovalId=T.ApprovalId
 WHERE T.SourceItemId<>R.SourceItemId OR EXISTS(SELECT T.PreviousCorrectionId EXCEPT SELECT R.PreviousCorrectionId) OR A.CorrectionId<>R.CorrectionId OR T.AfterBalance<>T.BeforeBalance+R.Delta OR dbo.SameAccountPerson(A.ApprovedByAccountId,R.CreatedByAccountId)=1 OR dbo.SameAccountPerson(A.ApprovedByAccountId,C.CreatedByAccountId)=1 OR dbo.AccountRepresentsPerson(A.ApprovedByAccountId,I.PersonId)=1
 OR NOT EXISTS(SELECT 1 FROM dbo.FinanceProjects F WHERE F.ProjectId=C.ProjectId AND F.IsActive=1)
 OR NOT EXISTS(SELECT 1 FROM dbo.PaymentLedger G WHERE G.LedgerId=T.LedgerId AND G.AccrualCorrectionId=R.CorrectionId AND G.Delta=R.Delta AND G.ReversedUtc IS NULL)
 OR NOT EXISTS(SELECT 1 FROM dbo.EmploymentPeriods E JOIN(SELECT PersonId,CurrentEmploymentId,ArchivedUtc,PARA,0 PersonnelGroup FROM dbo.Calisan UNION ALL SELECT PersonId,CurrentEmploymentId,ArchivedUtc,PARA,1 FROM dbo.Joker) V ON V.PersonId=E.PersonId AND V.CurrentEmploymentId=E.EmploymentId AND V.PersonnelGroup=E.PersonnelGroup WHERE E.EmploymentId=I.EmploymentId AND E.IsClosed=0 AND V.ArchivedUtc IS NULL AND V.PARA=T.AfterBalance)
 OR EXISTS(SELECT X.CorrectionId FROM(SELECT TOP(1) O.CorrectionId FROM dbo.AccrualCorrections O JOIN dbo.AccrualCorrectionTransfers Q ON Q.CorrectionId=O.CorrectionId WHERE O.SourceItemId=I.ItemId AND Q.[Sequence]<T.[Sequence] ORDER BY Q.[Sequence] DESC) X EXCEPT SELECT R.PreviousCorrectionId)
 OR (R.PreviousCorrectionId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.AccrualCorrections O JOIN dbo.AccrualCorrectionTransfers Q ON Q.CorrectionId=O.CorrectionId WHERE O.CorrectionId=R.PreviousCorrectionId AND O.SourceItemId=I.ItemId AND O.NewAmount=R.OldAmount AND Q.[Sequence]<T.[Sequence]))
 OR (R.PreviousCorrectionId IS NULL AND R.OldAmount<>I.Amount)) THROW 51277,N'Düzeltme onayı, güncel zincir, defter veya bakiye uzlaşmıyor.',1;
END;
GO
UPDATE dbo.admin SET SessionEpoch=NEWID();

GO
CREATE TRIGGER dbo.Audit_AccrualCorrections ON dbo.AccrualCorrections AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'AccrualCorrection.Change',N'AccrualCorrections',CONVERT(nvarchar(100),COALESCE(I.CorrectionId,D.CorrectionId)),CASE WHEN D.CorrectionId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',CASE WHEN D.CorrectionId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,CASE WHEN I.CorrectionId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application' FROM inserted I FULL OUTER JOIN deleted D ON D.CorrectionId=I.CorrectionId;
END;

GO
CREATE TRIGGER dbo.Audit_AccrualCorrectionApprovals ON dbo.AccrualCorrectionApprovals AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'AccrualCorrection.Change',N'AccrualCorrectionApprovals',CONVERT(nvarchar(100),COALESCE(I.ApprovalId,D.ApprovalId)),CASE WHEN D.ApprovalId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',CASE WHEN D.ApprovalId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,CASE WHEN I.ApprovalId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application' FROM inserted I FULL OUTER JOIN deleted D ON D.ApprovalId=I.ApprovalId;
END;

GO
CREATE TRIGGER dbo.Audit_AccrualCorrectionTransfers ON dbo.AccrualCorrectionTransfers AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'AccrualCorrection.Change',N'AccrualCorrectionTransfers',CONVERT(nvarchar(100),COALESCE(I.TransferId,D.TransferId)),CASE WHEN D.TransferId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',CASE WHEN D.TransferId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,CASE WHEN I.TransferId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application' FROM inserted I FULL OUTER JOIN deleted D ON D.TransferId=I.TransferId;
END;
