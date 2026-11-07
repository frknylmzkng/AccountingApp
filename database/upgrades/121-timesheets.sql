ALTER TABLE dbo.ProjectAccountAccess ADD CanEnterTimesheets bit NOT NULL CONSTRAINT DF_ProjectEnterTimesheets DEFAULT 0,
 CanApproveTimesheets bit NOT NULL CONSTRAINT DF_ProjectApproveTimesheets DEFAULT 0;
GO
CREATE TABLE dbo.Timesheets(
 TimesheetId uniqueidentifier NOT NULL PRIMARY KEY,
 ProjectId uniqueidentifier NOT NULL REFERENCES dbo.FinanceProjects(ProjectId),
 ShiftAssignmentId uniqueidentifier NOT NULL UNIQUE REFERENCES dbo.ShiftAssignments(ShiftAssignmentId),
 PersonId uniqueidentifier NOT NULL REFERENCES dbo.PersonnelIdentities(PersonId),
 EmploymentId uniqueidentifier NOT NULL REFERENCES dbo.EmploymentPeriods(EmploymentId),
 PersonnelGroup int NOT NULL CHECK(PersonnelGroup IN(0,1)),
 RosterAssignmentId uniqueidentifier NOT NULL REFERENCES dbo.PersonnelProjectAssignments(AssignmentId),
 WorkDate date NOT NULL,
 LocalStart datetime2(0) NULL,LocalEnd datetime2(0) NULL,StartUtc datetime2(0) NULL,EndUtc datetime2(0) NULL,
 TimeZoneId nvarchar(100) NOT NULL,PolicyVersion nvarchar(50) NOT NULL CONSTRAINT DF_TimesheetPolicy DEFAULT N'UTCElapsedMinutes.v1',
 BreakMinutes int NOT NULL CHECK(BreakMinutes>=0 AND BreakMinutes<1440),
 PayableMinutes int NULL CHECK(PayableMinutes>=0 AND PayableMinutes<=1440),
 ActualMinutes AS(CASE WHEN StartUtc<EndUtc THEN DATEDIFF(minute,StartUtc,EndUtc)-BreakMinutes END) PERSISTED,
 SourceKey nvarchar(100) NOT NULL,Status nvarchar(20) NOT NULL CONSTRAINT DF_TimesheetStatus DEFAULT N'Draft',
 CreatedByAccountId uniqueidentifier NOT NULL,CreatedBy nvarchar(50) NOT NULL,CreatedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
 EditedByAccountId uniqueidentifier NOT NULL,EditedBy nvarchar(50) NOT NULL,EditedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
 SubmittedByAccountId uniqueidentifier NULL,SubmittedUtc datetime2(7) NULL,
 DecidedByAccountId uniqueidentifier NULL,DecidedBy nvarchar(50) NULL,DecidedUtc datetime2(7) NULL,
 RecordVersion rowversion NOT NULL,
 CONSTRAINT UQ_TimesheetSource UNIQUE(ProjectId,SourceKey),
 CONSTRAINT CK_TimesheetTimes CHECK((LocalStart IS NULL AND StartUtc IS NULL OR LocalStart IS NOT NULL AND StartUtc IS NOT NULL) AND (LocalEnd IS NULL AND EndUtc IS NULL OR LocalEnd IS NOT NULL AND EndUtc IS NOT NULL)),
 CONSTRAINT CK_TimesheetState CHECK(Status IN(N'Draft',N'Submitted',N'Approved',N'Returned',N'Cancelled')),
 CONSTRAINT CK_TimesheetSubmitted CHECK(Status NOT IN(N'Submitted',N'Approved') OR (StartUtc IS NOT NULL AND EndUtc IS NOT NULL AND EndUtc>StartUtc AND DATEDIFF(minute,StartUtc,EndUtc)<=1440 AND BreakMinutes<DATEDIFF(minute,StartUtc,EndUtc) AND PayableMinutes IS NOT NULL AND PayableMinutes<=DATEDIFF(minute,StartUtc,EndUtc)-BreakMinutes AND SubmittedByAccountId IS NOT NULL AND SubmittedUtc IS NOT NULL)),
 CONSTRAINT CK_TimesheetApproval CHECK(Status<>N'Approved' OR (DecidedByAccountId IS NOT NULL AND DecidedUtc IS NOT NULL AND DecidedBy IS NOT NULL AND DecidedByAccountId<>CreatedByAccountId AND DecidedByAccountId<>EditedByAccountId AND DecidedByAccountId<>SubmittedByAccountId))
);
CREATE INDEX IX_TimesheetProjectDate ON dbo.Timesheets(ProjectId,WorkDate,TimesheetId) INCLUDE(Status,PersonId,ActualMinutes,PayableMinutes);
CREATE INDEX IX_TimesheetPersonRange ON dbo.Timesheets(PersonId,StartUtc,EndUtc) INCLUDE(Status,EmploymentId,RosterAssignmentId);
CREATE TABLE dbo.TimesheetCommands(
 CommandId uniqueidentifier NOT NULL PRIMARY KEY,ProjectId uniqueidentifier NOT NULL REFERENCES dbo.FinanceProjects(ProjectId),
 ActorId uniqueidentifier NOT NULL,PayloadHash binary(32) NOT NULL,CreatedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
CREATE TRIGGER dbo.GuardTimesheetCommands ON dbo.TimesheetCommands AFTER UPDATE,DELETE AS
BEGIN THROW 51130,N'Puantaj işlem kimliği değiştirilemez veya silinemez.',1; END;
GO
CREATE TRIGGER dbo.GuardTimesheets ON dbo.Timesheets AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.TimesheetId=D.TimesheetId WHERE I.TimesheetId IS NULL) THROW 51131,N'Puantaj silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I LEFT JOIN deleted D ON D.TimesheetId=I.TimesheetId WHERE D.TimesheetId IS NULL AND I.Status<>N'Draft') THROW 51132,N'Puantaj taslak başlamalı.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.TimesheetId=I.TimesheetId WHERE
 I.ProjectId<>D.ProjectId OR I.ShiftAssignmentId<>D.ShiftAssignmentId OR I.PersonId<>D.PersonId OR I.EmploymentId<>D.EmploymentId OR I.PersonnelGroup<>D.PersonnelGroup OR I.RosterAssignmentId<>D.RosterAssignmentId OR I.WorkDate<>D.WorkDate OR I.SourceKey<>D.SourceKey OR I.TimeZoneId<>D.TimeZoneId OR I.PolicyVersion<>D.PolicyVersion OR I.CreatedByAccountId<>D.CreatedByAccountId OR I.CreatedBy<>D.CreatedBy OR I.CreatedUtc<>D.CreatedUtc)
 THROW 51133,N'Puantajın kaynak kimliği değiştirilemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.TimesheetId=I.TimesheetId WHERE NOT(
 (D.Status IN(N'Draft',N'Returned') AND I.Status IN(N'Draft',N'Returned',N'Submitted',N'Cancelled')) OR
 (D.Status=N'Submitted' AND I.Status IN(N'Approved',N'Returned')) OR (D.Status=N'Approved' AND I.Status=N'Returned')))
 THROW 51134,N'Geçersiz puantaj durum geçişi.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.TimesheetId=I.TimesheetId WHERE (D.Status NOT IN(N'Draft',N'Returned') OR I.Status<>D.Status) AND EXISTS(
 SELECT I.LocalStart,I.LocalEnd,I.StartUtc,I.EndUtc,I.BreakMinutes,I.PayableMinutes,I.EditedByAccountId,I.EditedBy,I.EditedUtc
 EXCEPT SELECT D.LocalStart,D.LocalEnd,D.StartUtc,D.EndUtc,D.BreakMinutes,D.PayableMinutes,D.EditedByAccountId,D.EditedBy,D.EditedUtc))
 THROW 51135,N'Gönderilmiş puantaj gövdesi değiştirilemez; önce iade/yeniden açma gerekir.',1;
END;
GO
CREATE TRIGGER dbo.AuditTimesheets ON dbo.Timesheets AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'Timesheet.Change',N'Timesheets',CONVERT(nvarchar(100),COALESCE(I.TimesheetId,D.TimesheetId)),COALESCE(I.PersonId,D.PersonId),CASE WHEN D.TimesheetId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',
 CASE WHEN D.TimesheetId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 CASE WHEN I.TimesheetId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application'
 FROM inserted I FULL OUTER JOIN deleted D ON D.TimesheetId=I.TimesheetId;
END;
GO
CREATE OR ALTER FUNCTION dbo.AuditSafeJson(@json nvarchar(max)) RETURNS nvarchar(max) AS
BEGIN
 IF @json IS NULL RETURN NULL;
 IF ISJSON(@json)<>1 OR LEFT(LTRIM(@json),1)<>N'{' RETURN N'{"_privacy":"MASKELENDİ"}';
 DECLARE @body nvarchar(max);
 SELECT @body=STRING_AGG(CONVERT(nvarchar(max),N'"'+STRING_ESCAPE([key],'json')+N'":'+
 CASE WHEN [type]=0 THEN N'null'
 WHEN [key] COLLATE Latin1_General_100_BIN2 IN (N'PersonId',N'EmploymentId',N'CurrentEmploymentId',N'PersonnelGroup',N'IzinId',N'LedgerId',N'EntryId',N'ProjectId',N'TeamId',N'AssignmentId',N'GrantId',N'AccountId',N'FinanceEntryId',N'RequestId',N'ReversesLedgerId',N'RecordVersion',N'RoleName',N'IsActive',N'IsLegacy',N'IsClosed',N'IsCurrent',N'MustChangePassword',N'PasswordResetExpiresUtc',N'CanPay',N'CanExportPersonalData',N'CanViewSalary',N'CanViewIban',N'SourceShiftId',N'SourceShiftVersion',N'CopyBatchId',N'CanEnterTimesheets',N'CanApproveTimesheets',N'TimesheetId',N'ShiftAssignmentId',N'WorkDate',N'ActualMinutes',N'PayableMinutes',N'CreatedByAccountId',N'EditedByAccountId',N'SubmittedByAccountId',N'DecidedByAccountId',N'EditedUtc',N'SubmittedUtc',N'CanPlanShifts',N'CanApproveLeaves',N'ShiftId',N'TemplateId',N'LeaveId',N'StartUtc',N'EndUtc',N'LocalStart',N'LocalEnd',N'Status',N'BreakMinutes',N'RequiredCount',N'StartMinute',N'DurationMinutes',N'PolicyVersion',N'CanPreparePayments',N'CanApprovePayments',N'CredentialChanged',N'IsResponsible',N'CanManageAssignments',N'CanViewCosts',N'CanExport',N'StartDate',N'EndDate',N'PeriodStart',N'PeriodEnd',N'IZINBASLANGIC',N'IZINBITIS',N'CreatedUtc',N'ArchivedUtc',N'ReversedUtc',N'CancelledUtc',N'RevokedUtc',N'BusinessDate',N'TimeZoneId',N'RangeStartUtc',N'RangeEndUtc',N'ClosedUtc',N'PaymentCount',N'ReversalCount',N'CashOut',N'CashReturn',N'NetCash',N'DeclaredCash',N'Difference',N'SnapshotHash',N'Amount',N'PARA',N'Delta',N'Balance',N'Kind',N'PaymentPurpose',N'Rows',N'Format') AND [type] IN(1,2,3)
 THEN CASE WHEN [type]=1 THEN N'"'+STRING_ESCAPE(value,'json')+N'"' ELSE value END
 ELSE N'"[MASKELENDİ]"' END),N',') FROM OPENJSON(@json);
 RETURN N'{'+COALESCE(@body,N'')+N'}';
END;
GO
UPDATE dbo.admin SET SessionEpoch=NEWID();
