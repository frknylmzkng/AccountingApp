CREATE TABLE dbo.AccrualApprovals(
 ApprovalId uniqueidentifier NOT NULL PRIMARY KEY,CalculationId uniqueidentifier NOT NULL UNIQUE REFERENCES dbo.AccrualCalculations(CalculationId),
 ApprovedByAccountId uniqueidentifier NOT NULL,ApprovedBy nvarchar(50) NOT NULL,ApprovedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),PayloadHash binary(32) NOT NULL,RecordVersion rowversion NOT NULL
);
CREATE TABLE dbo.AccrualPostings(
 PostingId uniqueidentifier NOT NULL PRIMARY KEY,CalculationId uniqueidentifier NOT NULL UNIQUE REFERENCES dbo.AccrualCalculations(CalculationId),ApprovalId uniqueidentifier NOT NULL REFERENCES dbo.AccrualApprovals(ApprovalId),
 TotalAmount decimal(18,2) NOT NULL CHECK(TotalAmount>=0),ItemCount int NOT NULL CHECK(ItemCount BETWEEN 1 AND 2000),PayloadHash binary(32) NOT NULL,IsSealed bit NOT NULL DEFAULT 0,
 PostedByAccountId uniqueidentifier NOT NULL,PostedBy nvarchar(50) NOT NULL,PostedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),RecordVersion rowversion NOT NULL
);
ALTER TABLE dbo.FinanceEntries ADD AccrualCalculationId uniqueidentifier NULL REFERENCES dbo.AccrualCalculations(CalculationId),AccrualEmploymentId uniqueidentifier NULL;
GO
ALTER TABLE dbo.FinanceEntries ADD CONSTRAINT FK_FinanceAccrualEmployment FOREIGN KEY(AccrualEmploymentId,PersonId) REFERENCES dbo.EmploymentPeriods(EmploymentId,PersonId),
 CONSTRAINT CK_FinanceAccrualSource CHECK((AccrualCalculationId IS NULL AND AccrualEmploymentId IS NULL) OR (AccrualCalculationId IS NOT NULL AND AccrualEmploymentId IS NOT NULL AND Kind=N'Accrual'));
GO
CREATE UNIQUE INDEX UX_FinanceAccrualSource ON dbo.FinanceEntries(AccrualCalculationId,AccrualEmploymentId) WHERE AccrualCalculationId IS NOT NULL;
ALTER TABLE dbo.PaymentLedger ADD AccrualPostingId uniqueidentifier NULL REFERENCES dbo.AccrualPostings(PostingId);
GO
CREATE UNIQUE INDEX UX_LedgerAccrualEntry ON dbo.PaymentLedger(FinanceEntryId) WHERE AccrualPostingId IS NOT NULL;
CREATE TABLE dbo.AccrualPostingItems(
 ItemId uniqueidentifier NOT NULL PRIMARY KEY,PostingId uniqueidentifier NOT NULL REFERENCES dbo.AccrualPostings(PostingId),PersonId uniqueidentifier NOT NULL,
 EmploymentId uniqueidentifier NOT NULL,PersonnelGroup int NOT NULL CHECK(PersonnelGroup IN(0,1)),Amount decimal(18,2) NOT NULL CHECK(Amount>=0),
 BeforeBalance decimal(18,2) NOT NULL,AfterBalance decimal(18,2) NOT NULL,FinanceEntryId uniqueidentifier NULL REFERENCES dbo.FinanceEntries(EntryId),LedgerId bigint NULL REFERENCES dbo.PaymentLedger(LedgerId),
 CONSTRAINT FK_AccrualPostingEmployment FOREIGN KEY(EmploymentId,PersonId) REFERENCES dbo.EmploymentPeriods(EmploymentId,PersonId),
 CONSTRAINT UX_AccrualPostingEmployment UNIQUE(PostingId,EmploymentId),
 CONSTRAINT CK_AccrualPostingAmount CHECK(AfterBalance=BeforeBalance+Amount AND ((Amount=0 AND FinanceEntryId IS NULL AND LedgerId IS NULL) OR (Amount>0 AND FinanceEntryId IS NOT NULL AND LedgerId IS NOT NULL)))
);
-- A filtered unique key permits multiple explicit zero-value items (NULL finance links).
CREATE UNIQUE INDEX UX_AccrualItemFinance ON dbo.AccrualPostingItems(FinanceEntryId) WHERE FinanceEntryId IS NOT NULL;
CREATE UNIQUE INDEX UX_AccrualItemLedger ON dbo.AccrualPostingItems(LedgerId) WHERE LedgerId IS NOT NULL;
GO
CREATE TRIGGER dbo.GuardAccrualApproval ON dbo.AccrualApprovals AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51240,N'Mali onay değiştirilemez veya silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted A JOIN dbo.AccrualCalculations C ON C.CalculationId=A.CalculationId WHERE C.IsSealed<>1 OR dbo.SameAccountPerson(A.ApprovedByAccountId,C.CreatedByAccountId)=1 OR EXISTS(SELECT 1 FROM dbo.AccrualCalculationLines L WHERE L.CalculationId=C.CalculationId AND dbo.AccountRepresentsPerson(A.ApprovedByAccountId,L.PersonId)=1)) THROW 51241,N'Hazırlayan veya hak sahibiyle aynı kişi kendi dönem hesabını onaylayamaz.',1;
END;
GO
CREATE TRIGGER dbo.GuardAccrualPosting ON dbo.AccrualPostings AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.PostingId=D.PostingId WHERE I.PostingId IS NULL) THROW 51242,N'Aktarım geçmişi silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I LEFT JOIN deleted D ON D.PostingId=I.PostingId WHERE D.PostingId IS NULL AND I.IsSealed<>0) THROW 51243,N'Aktarım önce hazırlanmalı.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.PostingId=I.PostingId WHERE D.IsSealed<>0 OR I.IsSealed<>1 OR EXISTS(SELECT I.CalculationId,I.ApprovalId,I.TotalAmount,I.ItemCount,I.PayloadHash,I.PostedByAccountId,I.PostedBy COLLATE Latin1_General_100_BIN2,I.PostedUtc EXCEPT SELECT D.CalculationId,D.ApprovalId,D.TotalAmount,D.ItemCount,D.PayloadHash,D.PostedByAccountId,D.PostedBy COLLATE Latin1_General_100_BIN2,D.PostedUtc)) THROW 51244,N'Aktarım gövdesi değişmez.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN dbo.AccrualCalculations C ON C.CalculationId=I.CalculationId JOIN dbo.AccrualApprovals A ON A.ApprovalId=I.ApprovalId WHERE A.CalculationId<>C.CalculationId OR NOT EXISTS(SELECT 1 FROM dbo.FinanceProjects F WHERE F.ProjectId=C.ProjectId AND F.IsActive=1) OR C.IsSealed<>1 OR I.TotalAmount<>C.TotalAmount OR dbo.SameAccountPerson(A.ApprovedByAccountId,C.CreatedByAccountId)=1 OR EXISTS(SELECT 1 FROM dbo.AccrualCalculationLines L WHERE L.CalculationId=C.CalculationId AND dbo.AccountRepresentsPerson(A.ApprovedByAccountId,L.PersonId)=1)) THROW 51245,N'Aktarım geçerli bağımsız onaylı hesaba ait olmalı.',1;
 IF EXISTS(SELECT 1 FROM inserted I WHERE I.IsSealed=1 AND ((SELECT COUNT(*) FROM dbo.AccrualPostingItems X WHERE X.PostingId=I.PostingId)<>I.ItemCount OR (SELECT COUNT(DISTINCT L.EmploymentId) FROM dbo.AccrualCalculationLines L WHERE L.CalculationId=I.CalculationId)<>I.ItemCount OR (SELECT SUM(X.Amount) FROM dbo.AccrualPostingItems X WHERE X.PostingId=I.PostingId)<>I.TotalAmount)) THROW 51246,N'Aktarım toplamı ve çalışma dönemleri uzlaşmıyor.',1;
END;
GO
CREATE TRIGGER dbo.GuardAccrualPostingItem ON dbo.AccrualPostingItems AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51247,N'Aktarım satırı değiştirilemez veya silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN dbo.AccrualPostings P ON P.PostingId=I.PostingId JOIN dbo.EmploymentPeriods E ON E.EmploymentId=I.EmploymentId WHERE P.IsSealed<>0 OR E.PersonId<>I.PersonId OR E.PersonnelGroup<>I.PersonnelGroup OR E.IsClosed<>0 OR NOT EXISTS(SELECT 1 FROM(SELECT PersonId,CurrentEmploymentId,ArchivedUtc,PARA,0 PersonnelGroup FROM dbo.Calisan UNION ALL SELECT PersonId,CurrentEmploymentId,ArchivedUtc,PARA,1 FROM dbo.Joker) C WHERE C.PersonId=I.PersonId AND C.CurrentEmploymentId=I.EmploymentId AND C.PersonnelGroup=I.PersonnelGroup AND C.ArchivedUtc IS NULL AND C.PARA=I.AfterBalance) OR I.Amount<>(SELECT SUM(L.Amount) FROM dbo.AccrualCalculationLines L WHERE L.CalculationId=P.CalculationId AND L.EmploymentId=I.EmploymentId AND L.PersonId=I.PersonId)
 OR NOT EXISTS(SELECT 1 FROM dbo.AccrualCalculationLines L WHERE L.CalculationId=P.CalculationId AND L.EmploymentId=I.EmploymentId AND L.PersonId=I.PersonId)
 OR (I.Amount>0 AND NOT EXISTS(SELECT 1 FROM dbo.FinanceEntries F JOIN dbo.PaymentLedger G ON G.LedgerId=I.LedgerId AND G.FinanceEntryId=F.EntryId WHERE F.EntryId=I.FinanceEntryId AND F.AccrualCalculationId=P.CalculationId AND F.AccrualEmploymentId=I.EmploymentId AND F.PersonId=I.PersonId AND F.Amount=I.Amount AND G.Kind=N'Allocation' AND G.PersonId=I.PersonId AND G.PersonnelGroup=I.PersonnelGroup AND G.Delta=I.Amount AND G.AccrualPostingId=P.PostingId AND G.ReversedUtc IS NULL))) THROW 51248,N'Aktarım satırı kaynak/defter toplamıyla eşleşmiyor.',1;
END;
GO
CREATE TRIGGER dbo.GuardAccrualFinance ON dbo.FinanceEntries AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.EntryId=I.EntryId WHERE I.AccrualCalculationId IS NOT NULL) OR EXISTS(SELECT 1 FROM deleted WHERE AccrualCalculationId IS NOT NULL) THROW 51249,N'Dönem aktarım belgesi tek başına iptal edilemez; bağlantılı mali düzeltme gerekir.',1;
 IF EXISTS(SELECT 1 FROM inserted F WHERE F.AccrualCalculationId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.AccrualCalculations C JOIN dbo.AccrualApprovals A ON A.CalculationId=C.CalculationId JOIN dbo.AccrualPostings P ON P.CalculationId=C.CalculationId AND P.ApprovalId=A.ApprovalId WHERE C.CalculationId=F.AccrualCalculationId AND P.IsSealed=0 AND C.ProjectId=F.ProjectId AND F.PeriodStart=(SELECT MIN(L.WorkDate) FROM dbo.AccrualCalculationLines L WHERE L.CalculationId=C.CalculationId AND L.EmploymentId=F.AccrualEmploymentId AND L.PersonId=F.PersonId) AND F.PeriodEnd=(SELECT MAX(L.WorkDate) FROM dbo.AccrualCalculationLines L WHERE L.CalculationId=C.CalculationId AND L.EmploymentId=F.AccrualEmploymentId AND L.PersonId=F.PersonId) AND F.Amount=(SELECT SUM(L.Amount) FROM dbo.AccrualCalculationLines L WHERE L.CalculationId=C.CalculationId AND L.EmploymentId=F.AccrualEmploymentId AND L.PersonId=F.PersonId))) THROW 51250,N'Finans belgesi onaylı dönem aktarımına ait olmalı.',1;
END;
GO
CREATE TRIGGER dbo.GuardAccrualLedger ON dbo.PaymentLedger AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.LedgerId=I.LedgerId WHERE I.AccrualPostingId IS NOT NULL) OR EXISTS(SELECT 1 FROM deleted WHERE AccrualPostingId IS NOT NULL) THROW 51251,N'Dönem aktarım defteri değiştirilemez veya silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted G WHERE G.AccrualPostingId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.AccrualPostings P JOIN dbo.FinanceEntries F ON F.EntryId=G.FinanceEntryId AND F.AccrualCalculationId=P.CalculationId WHERE P.PostingId=G.AccrualPostingId AND P.IsSealed=0 AND G.Kind=N'Allocation' AND G.ProjectId=F.ProjectId AND G.PersonId=F.PersonId AND G.TC=F.TC AND G.PersonnelGroup=F.PersonnelGroup AND G.Delta=F.Amount AND G.ReversedUtc IS NULL)) THROW 51252,N'Defter satırı dönem aktarımına ait olmalı.',1;
END;
GO

CREATE TRIGGER dbo.Audit_AccrualApprovals ON dbo.AccrualApprovals AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'AccrualWorkflow.Change',N'AccrualApprovals',CONVERT(nvarchar(100),COALESCE(I.ApprovalId,D.ApprovalId)),CASE WHEN D.ApprovalId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',CASE WHEN D.ApprovalId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,CASE WHEN I.ApprovalId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application' FROM inserted I FULL OUTER JOIN deleted D ON D.ApprovalId=I.ApprovalId;
END;
GO
CREATE TRIGGER dbo.Audit_AccrualPostings ON dbo.AccrualPostings AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'AccrualWorkflow.Change',N'AccrualPostings',CONVERT(nvarchar(100),COALESCE(I.PostingId,D.PostingId)),CASE WHEN D.PostingId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',CASE WHEN D.PostingId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,CASE WHEN I.PostingId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application' FROM inserted I FULL OUTER JOIN deleted D ON D.PostingId=I.PostingId;
END;
GO
CREATE TRIGGER dbo.Audit_AccrualPostingItems ON dbo.AccrualPostingItems AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'AccrualWorkflow.Change',N'AccrualPostingItems',CONVERT(nvarchar(100),COALESCE(I.ItemId,D.ItemId)),CASE WHEN D.ItemId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',CASE WHEN D.ItemId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,CASE WHEN I.ItemId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application' FROM inserted I FULL OUTER JOIN deleted D ON D.ItemId=I.ItemId;
END;
GO
UPDATE dbo.admin SET SessionEpoch=NEWID();
