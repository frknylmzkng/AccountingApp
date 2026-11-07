CREATE TABLE dbo.PaymentAllocations(
 AllocationId uniqueidentifier NOT NULL PRIMARY KEY,PaymentLedgerId bigint NOT NULL REFERENCES dbo.PaymentLedger(LedgerId),
 FinanceEntryId uniqueidentifier NOT NULL REFERENCES dbo.FinanceEntries(EntryId),EmploymentId uniqueidentifier NOT NULL REFERENCES dbo.EmploymentPeriods(EmploymentId),
 Amount decimal(18,2) NOT NULL CHECK(Amount>0),Reason nvarchar(500) NOT NULL,
 CreatedByAccountId uniqueidentifier NOT NULL,CreatedBy nvarchar(50) NOT NULL,CreatedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),PayloadHash binary(32) NOT NULL
);
CREATE INDEX IX_PaymentAllocationSource ON dbo.PaymentAllocations(PaymentLedgerId) INCLUDE(Amount,FinanceEntryId);
CREATE INDEX IX_PaymentAllocationAccrual ON dbo.PaymentAllocations(FinanceEntryId) INCLUDE(Amount,PaymentLedgerId);
CREATE TABLE dbo.PaymentAllocationReleases(
 ReleaseId uniqueidentifier NOT NULL PRIMARY KEY,AllocationId uniqueidentifier NOT NULL UNIQUE REFERENCES dbo.PaymentAllocations(AllocationId),
 Reason nvarchar(500) NOT NULL,ReleasedByAccountId uniqueidentifier NOT NULL,ReleasedBy nvarchar(50) NOT NULL,ReleasedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),PayloadHash binary(32) NOT NULL
);
GO
CREATE VIEW dbo.ActivePaymentAllocations AS SELECT A.* FROM dbo.PaymentAllocations A WHERE NOT EXISTS(SELECT 1 FROM dbo.PaymentAllocationReleases R WHERE R.AllocationId=A.AllocationId);
GO
CREATE TRIGGER dbo.GuardPaymentAllocation ON dbo.PaymentAllocations AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51280,N'Tahsis kaydı değişmez; gerekçeyle serbest bırakın.',1;
 IF EXISTS(SELECT 1 FROM inserted A WHERE NOT EXISTS(
  SELECT 1 FROM dbo.PaymentLedger P WITH(UPDLOCK,HOLDLOCK) JOIN dbo.FinanceEffectiveEntries F WITH(HOLDLOCK) ON F.EntryId=A.FinanceEntryId
  JOIN dbo.EmploymentRecordLinks L ON L.RecordKind=N'Ledger' AND L.RecordKey=CONVERT(nvarchar(64),P.LedgerId)
  JOIN dbo.EmploymentRecordLinks E ON E.RecordKind=N'Finance' AND E.RecordKey=CONVERT(nvarchar(64),F.EntryId)
  WHERE P.LedgerId=A.PaymentLedgerId AND P.Kind=N'Payment' AND P.Delta<0 AND P.RequestId IS NOT NULL AND P.ReversedUtc IS NULL AND P.PaymentPurpose IN(N'Advance',N'Settlement')
  AND F.Kind=N'Accrual' AND F.ReversedUtc IS NULL AND F.PersonId=P.PersonId AND F.PersonnelGroup=P.PersonnelGroup AND F.ProjectId=P.ProjectId
  AND L.PersonId=P.PersonId AND E.PersonId=F.PersonId AND L.EmploymentId=E.EmploymentId AND A.EmploymentId=L.EmploymentId
  AND (SELECT SUM(X.Amount) FROM dbo.ActivePaymentAllocations X WITH(HOLDLOCK) WHERE X.PaymentLedgerId=P.LedgerId)<=-P.Delta
  AND (SELECT SUM(X.Amount) FROM dbo.ActivePaymentAllocations X WITH(HOLDLOCK) WHERE X.FinanceEntryId=F.EntryId)<=F.EffectiveAmount))
 THROW 51281,N'Tahsis ödeme/hakediş sınırı veya kişi/proje/çalışma dönemiyle eşleşmiyor.',1;
END;
GO
CREATE TRIGGER dbo.GuardPaymentAllocationRelease ON dbo.PaymentAllocationReleases AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;IF EXISTS(SELECT 1 FROM deleted) THROW 51282,N'Tahsis serbest bırakma geçmişi değişmez.',1;END;
GO
CREATE TRIGGER dbo.GuardAllocatedPayment ON dbo.PaymentLedger AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted P JOIN dbo.ActivePaymentAllocations A ON A.PaymentLedgerId=P.LedgerId)
 THROW 51283,N'Ödemeyi iptal etmeden önce aktif tahsislerini serbest bırakın.',1;
END;
GO
CREATE TRIGGER dbo.GuardAllocatedFinance ON dbo.FinanceEntries AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted F JOIN dbo.ActivePaymentAllocations A ON A.FinanceEntryId=F.EntryId)
 THROW 51284,N'Belgeyi iptal etmeden önce aktif tahsislerini serbest bırakın.',1;
END;
GO
CREATE TRIGGER dbo.GuardAllocatedCorrection ON dbo.AccrualCorrectionTransfers AFTER INSERT AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted T JOIN dbo.AccrualCorrections R ON R.CorrectionId=T.CorrectionId JOIN dbo.AccrualPostingItems I ON I.ItemId=R.SourceItemId WHERE R.NewAmount<COALESCE((SELECT SUM(A.Amount) FROM dbo.ActivePaymentAllocations A WITH(UPDLOCK,HOLDLOCK) WHERE A.FinanceEntryId=I.FinanceEntryId),0))
 THROW 51285,N'Düzeltilmiş hakediş aktif tahsis toplamının altına inemez; önce tahsisleri serbest bırakın.',1;
END;
GO
UPDATE dbo.admin SET SessionEpoch=NEWID();

GO
CREATE TRIGGER dbo.Audit_PaymentAllocations ON dbo.PaymentAllocations AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'PaymentAllocation.Change',N'PaymentAllocations',CONVERT(nvarchar(100),COALESCE(I.AllocationId,D.AllocationId)),CASE WHEN D.AllocationId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',CASE WHEN D.AllocationId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,CASE WHEN I.AllocationId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application' FROM inserted I FULL OUTER JOIN deleted D ON D.AllocationId=I.AllocationId;
END;

GO
CREATE TRIGGER dbo.Audit_PaymentAllocationReleases ON dbo.PaymentAllocationReleases AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'PaymentAllocation.Change',N'PaymentAllocationReleases',CONVERT(nvarchar(100),COALESCE(I.ReleaseId,D.ReleaseId)),CASE WHEN D.ReleaseId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',CASE WHEN D.ReleaseId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,CASE WHEN I.ReleaseId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application' FROM inserted I FULL OUTER JOIN deleted D ON D.ReleaseId=I.ReleaseId;
END;
