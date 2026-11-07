CREATE TABLE dbo.AttendanceEvents(
 EventId uniqueidentifier NOT NULL PRIMARY KEY,ProjectId uniqueidentifier NOT NULL REFERENCES dbo.FinanceProjects(ProjectId),
 PersonId uniqueidentifier NOT NULL REFERENCES dbo.PersonnelIdentities(PersonId),EmploymentId uniqueidentifier NOT NULL REFERENCES dbo.EmploymentPeriods(EmploymentId),
 PersonnelGroup int NOT NULL CHECK(PersonnelGroup IN(0,1)),RosterAssignmentId uniqueidentifier NOT NULL REFERENCES dbo.PersonnelProjectAssignments(AssignmentId),
 SourceSystem nvarchar(50) COLLATE Latin1_General_100_BIN2 NOT NULL,ExternalId nvarchar(100) COLLATE Latin1_General_100_BIN2 NOT NULL,
 Direction nvarchar(3) COLLATE Latin1_General_100_BIN2 NOT NULL CHECK(Direction IN(N'In',N'Out')),OccurredLocal datetime2(7) NOT NULL,OccurredUtc datetime2(7) NOT NULL,TimeZoneId nvarchar(100) NOT NULL,
 ImportedByAccountId uniqueidentifier NOT NULL,ImportedBy nvarchar(50) NOT NULL,ImportedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
 IgnoredUtc datetime2(7) NULL,IgnoredByAccountId uniqueidentifier NULL,IgnoredBy nvarchar(50) NULL,RecordVersion rowversion NOT NULL,
 CONSTRAINT UQ_AttendanceSource UNIQUE(SourceSystem,ExternalId),
 CONSTRAINT CK_AttendanceIgnore CHECK((IgnoredUtc IS NULL AND IgnoredByAccountId IS NULL AND IgnoredBy IS NULL) OR (IgnoredUtc IS NOT NULL AND IgnoredByAccountId IS NOT NULL AND IgnoredBy IS NOT NULL))
);
CREATE INDEX IX_AttendanceProjectTime ON dbo.AttendanceEvents(ProjectId,OccurredLocal,EventId) INCLUDE(PersonId,EmploymentId,Direction,IgnoredUtc);
CREATE TABLE dbo.AttendanceMatches(
 MatchId uniqueidentifier NOT NULL PRIMARY KEY,ProjectId uniqueidentifier NOT NULL REFERENCES dbo.FinanceProjects(ProjectId),
 TimesheetId uniqueidentifier NOT NULL REFERENCES dbo.Timesheets(TimesheetId),InEventId uniqueidentifier NOT NULL REFERENCES dbo.AttendanceEvents(EventId),OutEventId uniqueidentifier NOT NULL REFERENCES dbo.AttendanceEvents(EventId),
 MatchPolicy nvarchar(50) NOT NULL DEFAULT N'EventMinuteFloor.v1',
 MatchedByAccountId uniqueidentifier NOT NULL,MatchedBy nvarchar(50) NOT NULL,CreatedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
 ReleasedUtc datetime2(7) NULL,ReleasedByAccountId uniqueidentifier NULL,ReleasedBy nvarchar(50) NULL,RecordVersion rowversion NOT NULL,
 CONSTRAINT CK_AttendanceEventPair CHECK(InEventId<>OutEventId),
 CONSTRAINT CK_AttendanceRelease CHECK((ReleasedUtc IS NULL AND ReleasedByAccountId IS NULL AND ReleasedBy IS NULL) OR (ReleasedUtc IS NOT NULL AND ReleasedByAccountId IS NOT NULL AND ReleasedBy IS NOT NULL)),
 CONSTRAINT CK_AttendancePolicy CHECK(MatchPolicy=N'EventMinuteFloor.v1')
);
CREATE UNIQUE INDEX UX_AttendanceMatchTimesheet ON dbo.AttendanceMatches(TimesheetId) WHERE ReleasedUtc IS NULL;
CREATE UNIQUE INDEX UX_AttendanceMatchIn ON dbo.AttendanceMatches(InEventId) WHERE ReleasedUtc IS NULL;
CREATE UNIQUE INDEX UX_AttendanceMatchOut ON dbo.AttendanceMatches(OutEventId) WHERE ReleasedUtc IS NULL;
GO
CREATE TRIGGER dbo.GuardAttendanceEvents ON dbo.AttendanceEvents AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.EventId=D.EventId WHERE I.EventId IS NULL) THROW 51150,N'Ham olay silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I LEFT JOIN deleted D ON D.EventId=I.EventId WHERE D.EventId IS NULL AND I.IgnoredUtc IS NOT NULL) THROW 51151,N'Ham olay etkin başlamalı.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.EventId=I.EventId WHERE D.IgnoredUtc IS NOT NULL OR I.IgnoredUtc IS NULL OR EXISTS(
 SELECT I.ProjectId,I.PersonId,I.EmploymentId,I.PersonnelGroup,I.RosterAssignmentId,I.SourceSystem,I.ExternalId,I.Direction COLLATE Latin1_General_100_BIN2,I.OccurredLocal,I.OccurredUtc,I.TimeZoneId COLLATE Latin1_General_100_BIN2,I.ImportedByAccountId,I.ImportedBy COLLATE Latin1_General_100_BIN2,I.ImportedUtc
 EXCEPT SELECT D.ProjectId,D.PersonId,D.EmploymentId,D.PersonnelGroup,D.RosterAssignmentId,D.SourceSystem,D.ExternalId,D.Direction COLLATE Latin1_General_100_BIN2,D.OccurredLocal,D.OccurredUtc,D.TimeZoneId COLLATE Latin1_General_100_BIN2,D.ImportedByAccountId,D.ImportedBy COLLATE Latin1_General_100_BIN2,D.ImportedUtc)) THROW 51152,N'Ham olay gövdesi değişmez; yalnız gerekçeli yoksayılabilir.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN dbo.AttendanceMatches M ON M.InEventId=I.EventId OR M.OutEventId=I.EventId WHERE I.IgnoredUtc IS NOT NULL AND M.ReleasedUtc IS NULL) THROW 51153,N'Bağlı olay yoksayılamaz; önce eşlemeyi kaldırın.',1;
END;
GO
CREATE TRIGGER dbo.GuardAttendanceMatches ON dbo.AttendanceMatches AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.MatchId=D.MatchId WHERE I.MatchId IS NULL) THROW 51154,N'Eşleme geçmişi silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.MatchId=I.MatchId WHERE D.ReleasedUtc IS NOT NULL OR I.ReleasedUtc IS NULL OR EXISTS(
 SELECT I.ProjectId,I.TimesheetId,I.InEventId,I.OutEventId,I.MatchPolicy COLLATE Latin1_General_100_BIN2,I.MatchedByAccountId,I.MatchedBy COLLATE Latin1_General_100_BIN2,I.CreatedUtc
 EXCEPT SELECT D.ProjectId,D.TimesheetId,D.InEventId,D.OutEventId,D.MatchPolicy COLLATE Latin1_General_100_BIN2,D.MatchedByAccountId,D.MatchedBy COLLATE Latin1_General_100_BIN2,D.CreatedUtc)) THROW 51155,N'Eşleme kimliği değişmez; yalnız kaldırılabilir.',1;
 IF EXISTS(SELECT 1 FROM inserted M JOIN dbo.Timesheets T ON T.TimesheetId=M.TimesheetId WHERE T.Status NOT IN(N'Draft',N'Returned')) THROW 51156,N'Yalnız taslak/iade puantaj eşlemesi değişir.',1;
 IF EXISTS(SELECT 1 FROM inserted M LEFT JOIN deleted D ON D.MatchId=M.MatchId WHERE D.MatchId IS NULL AND M.ReleasedUtc IS NOT NULL) THROW 51157,N'Eşleme etkin başlamalı.',1;
 IF EXISTS(SELECT 1 FROM inserted M JOIN dbo.Timesheets T ON T.TimesheetId=M.TimesheetId JOIN dbo.AttendanceEvents A ON A.EventId=M.InEventId JOIN dbo.AttendanceEvents B ON B.EventId=M.OutEventId WHERE M.ReleasedUtc IS NULL AND
 (T.ProjectId<>M.ProjectId OR A.ProjectId<>M.ProjectId OR B.ProjectId<>M.ProjectId OR A.PersonId<>T.PersonId OR B.PersonId<>T.PersonId OR A.EmploymentId<>T.EmploymentId OR B.EmploymentId<>T.EmploymentId OR A.PersonnelGroup<>T.PersonnelGroup OR B.PersonnelGroup<>T.PersonnelGroup OR A.RosterAssignmentId<>T.RosterAssignmentId OR B.RosterAssignmentId<>T.RosterAssignmentId OR A.Direction<>N'In' OR B.Direction<>N'Out' OR A.IgnoredUtc IS NOT NULL OR B.IgnoredUtc IS NOT NULL OR A.OccurredUtc>=B.OccurredUtc OR CONVERT(date,A.OccurredLocal)<>T.WorkDate OR T.LocalStart IS NULL OR T.LocalEnd IS NULL OR T.LocalStart<>DATEADD(minute,DATEDIFF(minute,CONVERT(datetime2(7),CONVERT(date,A.OccurredLocal)),A.OccurredLocal),CONVERT(datetime2(7),CONVERT(date,A.OccurredLocal))) OR T.LocalEnd<>DATEADD(minute,DATEDIFF(minute,CONVERT(datetime2(7),CONVERT(date,B.OccurredLocal)),B.OccurredLocal),CONVERT(datetime2(7),CONVERT(date,B.OccurredLocal))) OR T.StartUtc IS NULL OR T.EndUtc IS NULL OR T.StartUtc<>DATEADD(minute,DATEDIFF(minute,CONVERT(datetime2(7),CONVERT(date,A.OccurredUtc)),A.OccurredUtc),CONVERT(datetime2(7),CONVERT(date,A.OccurredUtc))) OR T.EndUtc<>DATEADD(minute,DATEDIFF(minute,CONVERT(datetime2(7),CONVERT(date,B.OccurredUtc)),B.OccurredUtc),CONVERT(datetime2(7),CONVERT(date,B.OccurredUtc))))) THROW 51158,N'Olayların kişi/dönem/atama ve puantaj saatleri uyuşmuyor.',1;
END;
GO
CREATE TRIGGER dbo.GuardMatchedTimesheet ON dbo.Timesheets AFTER UPDATE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted T JOIN dbo.AttendanceMatches M ON M.TimesheetId=T.TimesheetId AND M.ReleasedUtc IS NULL JOIN dbo.AttendanceEvents A ON A.EventId=M.InEventId JOIN dbo.AttendanceEvents B ON B.EventId=M.OutEventId
 WHERE (T.Status=N'Approved' AND (T.DecidedByAccountId=M.MatchedByAccountId OR T.DecidedByAccountId=A.ImportedByAccountId OR T.DecidedByAccountId=B.ImportedByAccountId)) OR T.Status=N'Cancelled' OR T.LocalStart IS NULL OR T.LocalEnd IS NULL OR T.LocalStart<>DATEADD(minute,DATEDIFF(minute,CONVERT(datetime2(7),CONVERT(date,A.OccurredLocal)),A.OccurredLocal),CONVERT(datetime2(7),CONVERT(date,A.OccurredLocal))) OR T.LocalEnd<>DATEADD(minute,DATEDIFF(minute,CONVERT(datetime2(7),CONVERT(date,B.OccurredLocal)),B.OccurredLocal),CONVERT(datetime2(7),CONVERT(date,B.OccurredLocal))) OR T.StartUtc IS NULL OR T.EndUtc IS NULL OR T.StartUtc<>DATEADD(minute,DATEDIFF(minute,CONVERT(datetime2(7),CONVERT(date,A.OccurredUtc)),A.OccurredUtc),CONVERT(datetime2(7),CONVERT(date,A.OccurredUtc))) OR T.EndUtc<>DATEADD(minute,DATEDIFF(minute,CONVERT(datetime2(7),CONVERT(date,B.OccurredUtc)),B.OccurredUtc),CONVERT(datetime2(7),CONVERT(date,B.OccurredUtc)))) THROW 51159,N'Bağlı puantajın saatleri değişmez; önce eşlemeyi kaldırın.',1;
END;
GO
CREATE TRIGGER dbo.Audit_AttendanceEvents ON dbo.AttendanceEvents AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'Attendance.Change',N'AttendanceEvents',CONVERT(nvarchar(100),COALESCE(I.EventId,D.EventId)),COALESCE(I.PersonId,D.PersonId),CASE WHEN D.EventId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',
 CASE WHEN D.EventId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 CASE WHEN I.EventId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application'
 FROM inserted I FULL OUTER JOIN deleted D ON D.EventId=I.EventId;
END;
GO
CREATE TRIGGER dbo.Audit_AttendanceMatches ON dbo.AttendanceMatches AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'Attendance.Change',N'AttendanceMatches',CONVERT(nvarchar(100),COALESCE(I.MatchId,D.MatchId)),(SELECT T.PersonId FROM dbo.Timesheets T WHERE T.TimesheetId=COALESCE(I.TimesheetId,D.TimesheetId)),CASE WHEN D.MatchId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',
 CASE WHEN D.MatchId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 CASE WHEN I.MatchId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application'
 FROM inserted I FULL OUTER JOIN deleted D ON D.MatchId=I.MatchId;
END;
GO
CREATE OR ALTER FUNCTION dbo.AuditSafeJson(@json nvarchar(max)) RETURNS nvarchar(max) AS
BEGIN
 IF @json IS NULL RETURN NULL;
 IF ISJSON(@json)<>1 OR LEFT(LTRIM(@json),1)<>N'{' RETURN N'{"_privacy":"MASKELENDİ"}';
 DECLARE @body nvarchar(max);
 SELECT @body=STRING_AGG(CONVERT(nvarchar(max),N'"'+STRING_ESCAPE([key],'json')+N'":'+
 CASE WHEN [type]=0 THEN N'null'
 WHEN [key] COLLATE Latin1_General_100_BIN2 IN (N'EventId',N'MatchId',N'InEventId',N'OutEventId',N'RosterAssignmentId',N'Direction',N'OccurredLocal',N'OccurredUtc',N'ImportedByAccountId',N'MatchedByAccountId',N'ReleasedByAccountId',N'IgnoredByAccountId',N'ImportedUtc',N'IgnoredUtc',N'ReleasedUtc',N'MatchPolicy',N'PersonId',N'EmploymentId',N'CurrentEmploymentId',N'PersonnelGroup',N'IzinId',N'LedgerId',N'EntryId',N'ProjectId',N'TeamId',N'AssignmentId',N'GrantId',N'AccountId',N'FinanceEntryId',N'RequestId',N'ReversesLedgerId',N'RecordVersion',N'RoleName',N'IsActive',N'IsLegacy',N'IsClosed',N'IsCurrent',N'MustChangePassword',N'PasswordResetExpiresUtc',N'CanPay',N'CanExportPersonalData',N'CanViewSalary',N'CanViewIban',N'SourceShiftId',N'SourceShiftVersion',N'CopyBatchId',N'CanEnterTimesheets',N'CanApproveTimesheets',N'TimesheetId',N'ShiftAssignmentId',N'WorkDate',N'ActualMinutes',N'PayableMinutes',N'CreatedByAccountId',N'EditedByAccountId',N'SubmittedByAccountId',N'DecidedByAccountId',N'EditedUtc',N'SubmittedUtc',N'CanPlanShifts',N'CanApproveLeaves',N'ShiftId',N'TemplateId',N'LeaveId',N'StartUtc',N'EndUtc',N'LocalStart',N'LocalEnd',N'Status',N'BreakMinutes',N'RequiredCount',N'StartMinute',N'DurationMinutes',N'PolicyVersion',N'CanPreparePayments',N'CanApprovePayments',N'CredentialChanged',N'IsResponsible',N'CanManageAssignments',N'CanViewCosts',N'CanExport',N'StartDate',N'EndDate',N'PeriodStart',N'PeriodEnd',N'IZINBASLANGIC',N'IZINBITIS',N'CreatedUtc',N'ArchivedUtc',N'ReversedUtc',N'CancelledUtc',N'RevokedUtc',N'BusinessDate',N'TimeZoneId',N'RangeStartUtc',N'RangeEndUtc',N'ClosedUtc',N'PaymentCount',N'ReversalCount',N'CashOut',N'CashReturn',N'NetCash',N'DeclaredCash',N'Difference',N'SnapshotHash',N'Amount',N'PARA',N'Delta',N'Balance',N'Kind',N'PaymentPurpose',N'Rows',N'Format') AND [type] IN(1,2,3)
 THEN CASE WHEN [type]=1 THEN N'"'+STRING_ESCAPE(value,'json')+N'"' ELSE value END
 ELSE N'"[MASKELENDİ]"' END),N',') FROM OPENJSON(@json);
 RETURN N'{'+COALESCE(@body,N'')+N'}';
END;
GO
UPDATE dbo.admin SET SessionEpoch=NEWID();
