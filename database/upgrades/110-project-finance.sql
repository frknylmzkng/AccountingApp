-- Preserve existing balances and historical classification. Do not fabricate past accruals.
CREATE TABLE dbo.FinanceProjects(
 ProjectId uniqueidentifier NOT NULL CONSTRAINT PK_FinanceProjects PRIMARY KEY DEFAULT NEWID(),
 Code nvarchar(30) NOT NULL CONSTRAINT UX_FinanceProjectCode UNIQUE, Name nvarchar(100) NOT NULL,
 IsActive bit NOT NULL DEFAULT(1), CreatedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
 CreatedBy nvarchar(50) NOT NULL, RecordVersion rowversion NOT NULL);
CREATE TABLE dbo.FinanceEntries(
 EntryId uniqueidentifier NOT NULL CONSTRAINT PK_FinanceEntries PRIMARY KEY DEFAULT NEWID(),
 ProjectId uniqueidentifier NOT NULL REFERENCES dbo.FinanceProjects(ProjectId),
 Kind nvarchar(12) NOT NULL CHECK(Kind IN(N'Accrual',N'Expense')), Reference nvarchar(50) NOT NULL,
 PersonId uniqueidentifier NULL, TC bigint NULL, PersonnelGroup int NULL,
 PeriodStart date NOT NULL, PeriodEnd date NOT NULL, Amount decimal(18,2) NOT NULL CHECK(Amount>0),
 Description nvarchar(200) NOT NULL, CreatedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedBy nvarchar(50) NOT NULL,
 ReversedUtc datetime2 NULL, ReversedBy nvarchar(50) NULL, RecordVersion rowversion NOT NULL,
 CONSTRAINT FK_FinanceEntryPerson FOREIGN KEY(PersonId,TC) REFERENCES dbo.PersonnelIdentities(PersonId,TC),
 CONSTRAINT CK_FinanceEntryPeriod CHECK(PeriodEnd>=PeriodStart),
 CONSTRAINT CK_FinanceEntryType CHECK((Kind=N'Accrual' AND PersonId IS NOT NULL AND TC IS NOT NULL AND PersonnelGroup IS NOT NULL AND PersonnelGroup IN(0,1))
  OR (Kind=N'Expense' AND PersonId IS NULL AND TC IS NULL AND PersonnelGroup IS NULL)),
 CONSTRAINT CK_FinanceReversal CHECK((ReversedUtc IS NULL AND ReversedBy IS NULL) OR (ReversedUtc IS NOT NULL AND ReversedBy IS NOT NULL)),
 CONSTRAINT UX_FinanceReference UNIQUE(ProjectId,Kind,Reference));
CREATE INDEX IX_FinanceEntryPerson ON dbo.FinanceEntries(PersonId,PeriodStart);
ALTER TABLE dbo.PaymentLedger ADD PaymentPurpose nvarchar(20) NOT NULL CONSTRAINT DF_PaymentPurpose DEFAULT N'Unclassified',
 ProjectId uniqueidentifier NULL REFERENCES dbo.FinanceProjects(ProjectId),
 FinanceEntryId uniqueidentifier NULL REFERENCES dbo.FinanceEntries(EntryId);
GO
ALTER TABLE dbo.PaymentLedger ADD CONSTRAINT CK_PaymentPurpose CHECK(PaymentPurpose IN(N'Unclassified',N'Settlement',N'Advance'));
ALTER TABLE dbo.PaymentLedger ADD CONSTRAINT CK_AdvancePayment CHECK(PaymentPurpose<>N'Advance' OR (Kind=N'Payment' AND ProjectId IS NOT NULL AND Delta<0 AND RequestId IS NOT NULL));
CREATE INDEX IX_PaymentProject ON dbo.PaymentLedger(ProjectId,PaymentPurpose) INCLUDE(Delta,ReversedUtc,Kind);
-- New clients must supply the classification on every new payment.
UPDATE dbo.admin SET SessionEpoch=NEWID();
GO
CREATE TRIGGER dbo.ProtectFinanceEntries ON dbo.FinanceEntries AFTER UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.EntryId=D.EntryId WHERE I.EntryId IS NULL)
  THROW 51050,N'Finans belgesi silinemez; gerekçeli iptal kullanın.',1;
 IF EXISTS(SELECT 1 FROM deleted D INNER JOIN inserted I ON I.EntryId=D.EntryId WHERE
 D.ProjectId<>I.ProjectId OR D.Kind<>I.Kind OR D.Reference COLLATE Latin1_General_100_BIN2<>I.Reference COLLATE Latin1_General_100_BIN2
 OR ISNULL(D.PersonId,'00000000-0000-0000-0000-000000000000')<>ISNULL(I.PersonId,'00000000-0000-0000-0000-000000000000')
 OR ISNULL(D.TC,0)<>ISNULL(I.TC,0) OR ISNULL(D.PersonnelGroup,-1)<>ISNULL(I.PersonnelGroup,-1)
 OR D.PeriodStart<>I.PeriodStart OR D.PeriodEnd<>I.PeriodEnd OR D.Amount<>I.Amount
 OR D.Description COLLATE Latin1_General_100_BIN2<>I.Description COLLATE Latin1_General_100_BIN2
 OR D.CreatedUtc<>I.CreatedUtc OR D.CreatedBy COLLATE Latin1_General_100_BIN2<>I.CreatedBy COLLATE Latin1_General_100_BIN2
 OR D.ReversedUtc IS NOT NULL OR I.ReversedUtc IS NULL OR I.ReversedBy IS NULL)
 THROW 51051,N'Onaylı finans belgesi değiştirilemez; iptal ve yeni belge kullanın.',1;
END;
GO
CREATE TRIGGER dbo.AuditChanges_FinanceProjects ON dbo.FinanceProjects AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),
 N'Record.Change',N'Project',CONVERT(nvarchar(100),COALESCE(I.ProjectId,D.ProjectId)),NULL,
 CASE WHEN D.ProjectId IS NULL THEN N'Insert' WHEN I.ProjectId IS NULL THEN N'Delete' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),CASE WHEN SESSION_CONTEXT(N'Audit.Reason') IS NULL THEN N'NotProvided' ELSE N'UserProvided' END,
 J.BeforeJson,J.AfterJson,TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),CASE WHEN SESSION_CONTEXT(N'Audit.Actor') IS NULL THEN N'DirectSql' ELSE N'Application' END
 FROM inserted I FULL OUTER JOIN deleted D ON I.ProjectId=D.ProjectId
 CROSS APPLY(SELECT CASE WHEN D.ProjectId IS NULL THEN NULL ELSE (SELECT D.[ProjectId],D.[Code],D.[Name],D.[IsActive],D.[CreatedBy],D.[CreatedUtc] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END BeforeJson,
 CASE WHEN I.ProjectId IS NULL THEN NULL ELSE (SELECT I.[ProjectId],I.[Code],I.[Name],I.[IsActive],I.[CreatedBy],I.[CreatedUtc] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END AfterJson) J
 WHERE J.BeforeJson IS NULL OR J.AfterJson IS NULL OR J.BeforeJson COLLATE Latin1_General_100_BIN2<>J.AfterJson COLLATE Latin1_General_100_BIN2;
END;
GO
CREATE TRIGGER dbo.AuditChanges_FinanceEntries ON dbo.FinanceEntries AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),
 N'Record.Change',N'Finance.Entry',CONVERT(nvarchar(100),COALESCE(I.EntryId,D.EntryId)),COALESCE(I.PersonId,D.PersonId),
 CASE WHEN D.EntryId IS NULL THEN N'Insert' WHEN I.EntryId IS NULL THEN N'Delete' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),CASE WHEN SESSION_CONTEXT(N'Audit.Reason') IS NULL THEN N'NotProvided' ELSE N'UserProvided' END,
 J.BeforeJson,J.AfterJson,TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),CASE WHEN SESSION_CONTEXT(N'Audit.Actor') IS NULL THEN N'DirectSql' ELSE N'Application' END
 FROM inserted I FULL OUTER JOIN deleted D ON I.EntryId=D.EntryId
 CROSS APPLY(SELECT CASE WHEN D.EntryId IS NULL THEN NULL ELSE (SELECT D.[EntryId],D.[ProjectId],D.[Kind],D.[Reference],D.[PersonId],D.[TC],D.[PersonnelGroup],D.[PeriodStart],D.[PeriodEnd],D.[Amount],D.[Description],D.[CreatedBy],D.[CreatedUtc],D.[ReversedUtc],D.[ReversedBy] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END BeforeJson,
 CASE WHEN I.EntryId IS NULL THEN NULL ELSE (SELECT I.[EntryId],I.[ProjectId],I.[Kind],I.[Reference],I.[PersonId],I.[TC],I.[PersonnelGroup],I.[PeriodStart],I.[PeriodEnd],I.[Amount],I.[Description],I.[CreatedBy],I.[CreatedUtc],I.[ReversedUtc],I.[ReversedBy] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END AfterJson) J
 WHERE J.BeforeJson IS NULL OR J.AfterJson IS NULL OR J.BeforeJson COLLATE Latin1_General_100_BIN2<>J.AfterJson COLLATE Latin1_General_100_BIN2;
END;
GO

CREATE OR ALTER TRIGGER dbo.AuditChanges_PaymentLedger ON dbo.[PaymentLedger] AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),
 N'Record.Change',N'Payment',CONVERT(nvarchar(100),COALESCE(I.[LedgerId],D.[LedgerId])),COALESCE(I.PersonId,D.PersonId),
 CASE WHEN D.[LedgerId] IS NULL THEN N'Insert' WHEN I.[LedgerId] IS NULL THEN N'Delete' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),
 CASE WHEN SESSION_CONTEXT(N'Audit.Reason') IS NULL THEN N'NotProvided' ELSE N'UserProvided' END,
 J.BeforeJson,J.AfterJson,TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),
 CASE WHEN SESSION_CONTEXT(N'Audit.Actor') IS NULL THEN N'DirectSql' ELSE N'Application' END
 FROM inserted I FULL OUTER JOIN deleted D ON I.[LedgerId]=D.[LedgerId]
 CROSS APPLY(SELECT CASE WHEN D.[LedgerId] IS NULL THEN NULL ELSE (SELECT D.[LedgerId],D.[PersonId],D.[TC],D.[PersonnelGroup],D.[Delta],D.[Kind],D.[Actor],D.[RequestId],D.[ReversedUtc],D.[PaymentPurpose],D.[ProjectId],D.[FinanceEntryId] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END BeforeJson,
 CASE WHEN I.[LedgerId] IS NULL THEN NULL ELSE (SELECT I.[LedgerId],I.[PersonId],I.[TC],I.[PersonnelGroup],I.[Delta],I.[Kind],I.[Actor],I.[RequestId],I.[ReversedUtc],I.[PaymentPurpose],I.[ProjectId],I.[FinanceEntryId] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END AfterJson) J
 WHERE J.BeforeJson IS NULL OR J.AfterJson IS NULL OR J.BeforeJson COLLATE Latin1_General_100_BIN2<>J.AfterJson COLLATE Latin1_General_100_BIN2;
END;
GO

