ALTER TABLE dbo.ProjectAccountAccess ADD CanPreparePayments bit NOT NULL CONSTRAINT DF_ProjectPreparePayments DEFAULT 0,
 CanApprovePayments bit NOT NULL CONSTRAINT DF_ProjectApprovePayments DEFAULT 0;
GO
ALTER TABLE dbo.ProjectAccountAccess ADD CONSTRAINT CK_ProjectPaymentCosts CHECK((CanPreparePayments=0 AND CanApprovePayments=0) OR CanViewCosts=1);
CREATE TABLE dbo.ProjectPaymentProposals(
 RequestId uniqueidentifier NOT NULL CONSTRAINT PK_ProjectPaymentProposals PRIMARY KEY,
 ProjectId uniqueidentifier NOT NULL REFERENCES dbo.FinanceProjects(ProjectId),
 PersonId uniqueidentifier NOT NULL REFERENCES dbo.PersonnelIdentities(PersonId),
 EmploymentId uniqueidentifier NOT NULL, PersonnelGroup int NOT NULL CHECK(PersonnelGroup IN(0,1)), TC bigint NOT NULL,
 Amount decimal(18,2) NOT NULL CHECK(Amount>0), Purpose nvarchar(20) NOT NULL CHECK(Purpose IN(N'Settlement',N'Advance')),
 PersonVersion binary(8) NOT NULL, ClosureId bigint NOT NULL,
 PreparedByAccountId uniqueidentifier NOT NULL, PreparedBy nvarchar(50) NOT NULL, PreparedEpoch uniqueidentifier NOT NULL,
 PreparedGrantId uniqueidentifier NULL, PreparedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), PrepareReason nvarchar(500) NOT NULL,
 Status nvarchar(12) NOT NULL DEFAULT N'Pending', DecidedByAccountId uniqueidentifier NULL, DecidedBy nvarchar(50) NULL,
 DecidedUtc datetime2 NULL, DecisionReason nvarchar(500) NULL, DecisionVersion binary(8) NULL,
 LedgerId bigint NULL REFERENCES dbo.PaymentLedger(LedgerId), RecordVersion rowversion NOT NULL,
 CONSTRAINT FK_ProposalIdentity FOREIGN KEY(PersonId,TC) REFERENCES dbo.PersonnelIdentities(PersonId,TC),
 CONSTRAINT FK_ProposalEmployment FOREIGN KEY(EmploymentId,PersonId) REFERENCES dbo.EmploymentPeriods(EmploymentId,PersonId),
 CONSTRAINT CK_ProposalDifferentActor CHECK(DecidedByAccountId IS NULL OR DecidedByAccountId<>PreparedByAccountId),
 CONSTRAINT CK_ProposalState CHECK(
 (Status=N'Pending' AND DecidedByAccountId IS NULL AND DecidedBy IS NULL AND DecidedUtc IS NULL AND DecisionReason IS NULL AND DecisionVersion IS NULL AND LedgerId IS NULL)
 OR (Status IN(N'Approved',N'Rejected') AND DecidedByAccountId IS NOT NULL AND DecidedBy IS NOT NULL AND DecidedUtc IS NOT NULL AND DecisionReason IS NOT NULL AND DecisionVersion IS NOT NULL
 AND ((Status=N'Approved' AND LedgerId IS NOT NULL) OR (Status=N'Rejected' AND LedgerId IS NULL)))));
CREATE INDEX IX_ProposalProject ON dbo.ProjectPaymentProposals(ProjectId,PreparedUtc DESC,RequestId);
GO
CREATE TRIGGER dbo.ProtectProjectPaymentProposals ON dbo.ProjectPaymentProposals AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.RequestId=D.RequestId WHERE I.RequestId IS NULL) THROW 51110,N'Ödeme talebi silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.RequestId=I.RequestId
 WHERE D.Status<>N'Pending' OR I.Status NOT IN(N'Approved',N'Rejected') OR
 (SELECT D.ProjectId,D.PersonId,D.EmploymentId,D.PersonnelGroup,D.TC,D.Amount,D.Purpose,D.PersonVersion,D.ClosureId,D.PreparedByAccountId,D.PreparedBy,D.PreparedEpoch,D.PreparedGrantId,D.PreparedUtc,D.PrepareReason FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) COLLATE Latin1_General_100_BIN2<>
 (SELECT I.ProjectId,I.PersonId,I.EmploymentId,I.PersonnelGroup,I.TC,I.Amount,I.Purpose,I.PersonVersion,I.ClosureId,I.PreparedByAccountId,I.PreparedBy,I.PreparedEpoch,I.PreparedGrantId,I.PreparedUtc,I.PrepareReason FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) COLLATE Latin1_General_100_BIN2)
 THROW 51111,N'Ödeme talebinin niyeti ve sonuçlanmış kararı değiştirilemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I LEFT JOIN dbo.PaymentLedger L ON L.LedgerId=I.LedgerId
 WHERE I.Status=N'Approved' AND (L.LedgerId IS NULL OR L.Kind<>N'Payment' OR L.RequestId IS NULL OR L.RequestId<>I.RequestId OR L.ProjectId IS NULL OR L.ProjectId<>I.ProjectId OR L.PersonId IS NULL OR L.PersonId<>I.PersonId OR L.PersonnelGroup<>I.PersonnelGroup OR L.Delta<>-I.Amount OR L.PaymentPurpose<>I.Purpose OR NOT EXISTS(SELECT 1 FROM dbo.EmploymentRecordLinks E WHERE E.RecordKind=N'Ledger' AND E.RecordKey=CONVERT(nvarchar(64),L.LedgerId) AND E.EmploymentId=I.EmploymentId AND E.PersonId=I.PersonId)))
 THROW 51112,N'Onay ve ödeme kaydı eşleşmiyor.',1;
END;
GO
CREATE TRIGGER dbo.AuditProjectPaymentProposals ON dbo.ProjectPaymentProposals AFTER INSERT,UPDATE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'ProjectPayment.Change',N'ProjectPayment',CONVERT(nvarchar(100),I.RequestId),I.PersonId,CASE WHEN D.RequestId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',
 CASE WHEN D.RequestId IS NULL THEN NULL ELSE (SELECT D.ProjectId,D.PersonId,D.EmploymentId,D.Amount,D.Purpose PaymentPurpose,D.Status Kind,D.LedgerId FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 (SELECT I.ProjectId,I.PersonId,I.EmploymentId,I.Amount,I.Purpose PaymentPurpose,I.Status Kind,I.LedgerId FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER),
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application'
 FROM inserted I LEFT JOIN deleted D ON D.RequestId=I.RequestId;
END;
GO
CREATE OR ALTER FUNCTION dbo.AuditSafeJson(@json nvarchar(max)) RETURNS nvarchar(max) AS
BEGIN
 IF @json IS NULL RETURN NULL;
 IF ISJSON(@json)<>1 OR LEFT(LTRIM(@json),1)<>N'{' RETURN N'{"_privacy":"MASKELENDİ"}';
 DECLARE @body nvarchar(max);
 SELECT @body=STRING_AGG(CONVERT(nvarchar(max),N'"'+STRING_ESCAPE([key],'json')+N'":'+
 CASE WHEN [type]=0 THEN N'null'
 WHEN [key] COLLATE Latin1_General_100_BIN2 IN (N'PersonId',N'EmploymentId',N'CurrentEmploymentId',N'PersonnelGroup',N'IzinId',N'LedgerId',N'EntryId',N'ProjectId',N'TeamId',N'AssignmentId',N'GrantId',N'AccountId',N'FinanceEntryId',N'RequestId',N'ReversesLedgerId',N'RecordVersion',N'RoleName',N'IsActive',N'IsLegacy',N'IsClosed',N'IsCurrent',N'MustChangePassword',N'PasswordResetExpiresUtc',N'CanPay',N'CanExportPersonalData',N'CanViewSalary',N'CanViewIban',N'CanPreparePayments',N'CanApprovePayments',N'CredentialChanged',N'IsResponsible',N'CanManageAssignments',N'CanViewCosts',N'CanExport',N'StartDate',N'EndDate',N'PeriodStart',N'PeriodEnd',N'IZINBASLANGIC',N'IZINBITIS',N'CreatedUtc',N'ArchivedUtc',N'ReversedUtc',N'CancelledUtc',N'RevokedUtc',N'BusinessDate',N'TimeZoneId',N'RangeStartUtc',N'RangeEndUtc',N'ClosedUtc',N'PaymentCount',N'ReversalCount',N'CashOut',N'CashReturn',N'NetCash',N'DeclaredCash',N'Difference',N'SnapshotHash',N'Amount',N'PARA',N'Delta',N'Balance',N'Kind',N'PaymentPurpose',N'Rows',N'Format') AND [type] IN(1,2,3)
 THEN CASE WHEN [type]=1 THEN N'"'+STRING_ESCAPE(value,'json')+N'"' ELSE value END
 ELSE N'"[MASKELENDİ]"' END),N',') FROM OPENJSON(@json);
 RETURN N'{'+COALESCE(@body,N'')+N'}';
END;
GO
UPDATE dbo.admin SET SessionEpoch=NEWID();
