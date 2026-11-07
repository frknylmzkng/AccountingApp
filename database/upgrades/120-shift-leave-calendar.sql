ALTER TABLE dbo.ProjectAccountAccess ADD CanPlanShifts bit NOT NULL CONSTRAINT DF_ProjectPlanShifts DEFAULT 0,
 CanApproveLeaves bit NOT NULL CONSTRAINT DF_ProjectApproveLeaves DEFAULT 0;
GO
CREATE TABLE dbo.ShiftTemplates(
 TemplateId uniqueidentifier NOT NULL PRIMARY KEY, ProjectId uniqueidentifier NOT NULL REFERENCES dbo.FinanceProjects(ProjectId),
 TeamId uniqueidentifier NULL, Name nvarchar(100) NOT NULL, Site nvarchar(100) NOT NULL,
 StartMinute int NOT NULL CHECK(StartMinute>=0 AND StartMinute<1440),DurationMinutes int NOT NULL CHECK(DurationMinutes>0 AND DurationMinutes<=1440),
 BreakMinutes int NOT NULL CHECK(BreakMinutes>=0),RequiredCount int NOT NULL CHECK(RequiredCount BETWEEN 1 AND 500),
 IsActive bit NOT NULL DEFAULT 1,RecordVersion rowversion NOT NULL,
 CONSTRAINT CK_ShiftTemplateBreak CHECK(BreakMinutes<DurationMinutes),
 CONSTRAINT FK_ShiftTemplateTeam FOREIGN KEY(TeamId,ProjectId) REFERENCES dbo.ProjectTeams(TeamId,ProjectId));
CREATE INDEX IX_ShiftTemplateProject ON dbo.ShiftTemplates(ProjectId,TemplateId);
CREATE TABLE dbo.ScheduleCopyBatches(
 BatchId uniqueidentifier NOT NULL PRIMARY KEY,ProjectId uniqueidentifier NOT NULL REFERENCES dbo.FinanceProjects(ProjectId),
 SourceWeek date NOT NULL,TargetWeek date NOT NULL,PayloadHash binary(32) NOT NULL,ActorAccountId uniqueidentifier NOT NULL,
 CreatedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),Reason nvarchar(500) NOT NULL);
CREATE TABLE dbo.PlannedShifts(
 ShiftId uniqueidentifier NOT NULL PRIMARY KEY,ProjectId uniqueidentifier NOT NULL REFERENCES dbo.FinanceProjects(ProjectId),
 CopyBatchId uniqueidentifier NULL REFERENCES dbo.ScheduleCopyBatches(BatchId),SourceShiftId uniqueidentifier NULL REFERENCES dbo.PlannedShifts(ShiftId),SourceShiftVersion binary(8) NULL,TemplateId uniqueidentifier NOT NULL REFERENCES dbo.ShiftTemplates(TemplateId),TemplateVersion binary(8) NOT NULL,
 TeamId uniqueidentifier NULL,Name nvarchar(100) NOT NULL,Site nvarchar(100) NOT NULL,
 LocalStart datetime2 NOT NULL,LocalEnd datetime2 NOT NULL,StartUtc datetime2 NOT NULL,EndUtc datetime2 NOT NULL,
 PolicyVersion nvarchar(40) NOT NULL DEFAULT N'LocalPlannedMinutes.v1',TimeZoneId nvarchar(100) NOT NULL,BreakMinutes int NOT NULL,RequiredCount int NOT NULL,
 Status nvarchar(12) NOT NULL DEFAULT N'Draft',CreatedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),CreatedBy nvarchar(50) NOT NULL,CreateReason nvarchar(500) NOT NULL,
 PublishedUtc datetime2 NULL,PublishedBy nvarchar(50) NULL,CancelledUtc datetime2 NULL,CancelledBy nvarchar(50) NULL,RecordVersion rowversion NOT NULL,
 CONSTRAINT CK_ShiftCopyPair CHECK((CopyBatchId IS NULL AND SourceShiftId IS NULL AND SourceShiftVersion IS NULL) OR(CopyBatchId IS NOT NULL AND SourceShiftId IS NOT NULL AND SourceShiftVersion IS NOT NULL)),
 CONSTRAINT CK_ShiftTime CHECK(LocalStart<LocalEnd AND StartUtc<EndUtc AND DATEDIFF(minute,LocalStart,LocalEnd)<=1440 AND BreakMinutes>=0 AND BreakMinutes<DATEDIFF(minute,LocalStart,LocalEnd) AND RequiredCount BETWEEN 1 AND 500),
 CONSTRAINT CK_ShiftState CHECK((Status=N'Draft' AND PublishedUtc IS NULL AND PublishedBy IS NULL AND CancelledUtc IS NULL AND CancelledBy IS NULL)
 OR(Status=N'Published' AND PublishedUtc IS NOT NULL AND PublishedBy IS NOT NULL AND CancelledUtc IS NULL AND CancelledBy IS NULL)
 OR(Status=N'Cancelled' AND CancelledUtc IS NOT NULL AND CancelledBy IS NOT NULL AND ((PublishedUtc IS NULL AND PublishedBy IS NULL) OR(PublishedUtc IS NOT NULL AND PublishedBy IS NOT NULL)))),
 CONSTRAINT FK_ShiftTeam FOREIGN KEY(TeamId,ProjectId) REFERENCES dbo.ProjectTeams(TeamId,ProjectId));
CREATE INDEX IX_ShiftWeek ON dbo.PlannedShifts(ProjectId,LocalStart,ShiftId) INCLUDE(LocalEnd,Status,TeamId);
CREATE TABLE dbo.ShiftAssignments(
 ShiftAssignmentId uniqueidentifier NOT NULL PRIMARY KEY,ShiftId uniqueidentifier NOT NULL REFERENCES dbo.PlannedShifts(ShiftId),
 PersonId uniqueidentifier NOT NULL REFERENCES dbo.PersonnelIdentities(PersonId),EmploymentId uniqueidentifier NOT NULL,
 PersonnelGroup int NOT NULL CHECK(PersonnelGroup IN(0,1)),RosterAssignmentId uniqueidentifier NOT NULL REFERENCES dbo.PersonnelProjectAssignments(AssignmentId),
 CreatedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),CreatedBy nvarchar(50) NOT NULL,
 CancelledUtc datetime2 NULL,CancelledBy nvarchar(50) NULL,RecordVersion rowversion NOT NULL,
 CONSTRAINT FK_ShiftEmployment FOREIGN KEY(EmploymentId,PersonId) REFERENCES dbo.EmploymentPeriods(EmploymentId,PersonId),
 CONSTRAINT CK_ShiftAssignmentCancel CHECK((CancelledUtc IS NULL AND CancelledBy IS NULL) OR(CancelledUtc IS NOT NULL AND CancelledBy IS NOT NULL)));
CREATE UNIQUE INDEX UX_ShiftActivePerson ON dbo.ShiftAssignments(ShiftId,PersonId) WHERE CancelledUtc IS NULL;
CREATE INDEX IX_ShiftPerson ON dbo.ShiftAssignments(PersonId,ShiftId) INCLUDE(EmploymentId,CancelledUtc,RosterAssignmentId);
CREATE TABLE dbo.LeaveRequests(
 LeaveId uniqueidentifier NOT NULL PRIMARY KEY,ProjectId uniqueidentifier NOT NULL REFERENCES dbo.FinanceProjects(ProjectId),
 PersonId uniqueidentifier NOT NULL REFERENCES dbo.PersonnelIdentities(PersonId),EmploymentId uniqueidentifier NOT NULL,
 PersonnelGroup int NOT NULL CHECK(PersonnelGroup IN(0,1)),PersonVersion binary(8) NOT NULL,RosterAssignmentId uniqueidentifier NOT NULL REFERENCES dbo.PersonnelProjectAssignments(AssignmentId),
 LocalStart datetime2 NOT NULL,LocalEnd datetime2 NOT NULL,StartUtc datetime2 NOT NULL,EndUtc datetime2 NOT NULL,TimeZoneId nvarchar(100) NOT NULL,
 PolicyVersion nvarchar(40) NOT NULL DEFAULT N'CalendarMinutes.v1',
 Status nvarchar(12) NOT NULL DEFAULT N'Requested',CreatedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),CreatedBy nvarchar(50) NOT NULL,CreateReason nvarchar(500) NOT NULL,
 DecidedUtc datetime2 NULL,DecidedBy nvarchar(50) NULL,DecisionReason nvarchar(500) NULL,
 CancelledUtc datetime2 NULL,CancelledBy nvarchar(50) NULL,CancelReason nvarchar(500) NULL,RecordVersion rowversion NOT NULL,
 CONSTRAINT FK_LeaveRequestEmployment FOREIGN KEY(EmploymentId,PersonId) REFERENCES dbo.EmploymentPeriods(EmploymentId,PersonId),
 CONSTRAINT CK_LeaveRequestRange CHECK(LocalStart<LocalEnd AND StartUtc<EndUtc AND DATEDIFF(day,LocalStart,LocalEnd)<=366),
 CONSTRAINT CK_LeaveRequestState CHECK((Status=N'Requested' AND DecidedUtc IS NULL AND DecidedBy IS NULL AND DecisionReason IS NULL AND CancelledUtc IS NULL)
 OR(Status IN(N'Approved',N'Rejected') AND DecidedUtc IS NOT NULL AND DecidedBy IS NOT NULL AND DecisionReason IS NOT NULL AND CancelledUtc IS NULL)
 OR(Status=N'Cancelled' AND CancelledUtc IS NOT NULL AND CancelledBy IS NOT NULL AND CancelReason IS NOT NULL)));
CREATE INDEX IX_LeaveRequestWeek ON dbo.LeaveRequests(ProjectId,LocalStart,LeaveId) INCLUDE(LocalEnd,PersonId,EmploymentId,Status);
CREATE INDEX IX_LeaveRequestPerson ON dbo.LeaveRequests(PersonId,StartUtc,EndUtc) INCLUDE(EmploymentId,Status);
GO
CREATE TRIGGER dbo.ProtectScheduleCopyBatches ON dbo.ScheduleCopyBatches INSTEAD OF UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;IF EXISTS(SELECT 1 FROM deleted) THROW 51120,N'Toplu kopyalama niyeti değiştirilemez.',1;END;
GO
CREATE TRIGGER dbo.ProtectCalendarTemplateDelete ON dbo.ShiftTemplates INSTEAD OF DELETE AS
BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted) THROW 51120,N'Şablon silinemez; pasife alın.',1; END;
GO
ALTER TABLE dbo.LeaveRequests ADD CONSTRAINT CK_LeaveCancelPair CHECK((CancelledUtc IS NULL AND CancelledBy IS NULL AND CancelReason IS NULL) OR(CancelledUtc IS NOT NULL AND CancelledBy IS NOT NULL AND CancelReason IS NOT NULL)),
 CONSTRAINT CK_LeaveDecisionPair CHECK((DecidedUtc IS NULL AND DecidedBy IS NULL AND DecisionReason IS NULL) OR(DecidedUtc IS NOT NULL AND DecidedBy IS NOT NULL AND DecisionReason IS NOT NULL));
GO
CREATE TRIGGER dbo.ProtectCalendar_PlannedShifts ON dbo.PlannedShifts AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.ShiftId=D.ShiftId WHERE I.ShiftId IS NULL) THROW 51120,N'Takvim geçmişi silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.ShiftId=I.ShiftId WHERE (D.Status=N'Cancelled' OR (D.Status=N'Published' AND I.Status<>N'Cancelled')) OR
 (SELECT D.CopyBatchId,D.SourceShiftId,D.SourceShiftVersion,D.ProjectId,D.TemplateId,D.TemplateVersion,D.TeamId,D.Name,D.Site,D.LocalStart,D.LocalEnd,D.StartUtc,D.EndUtc,D.TimeZoneId,D.PolicyVersion,D.BreakMinutes,D.RequiredCount,D.CreatedUtc,D.CreatedBy,D.CreateReason FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) COLLATE Latin1_General_100_BIN2<>
 (SELECT I.CopyBatchId,I.SourceShiftId,I.SourceShiftVersion,I.ProjectId,I.TemplateId,I.TemplateVersion,I.TeamId,I.Name,I.Site,I.LocalStart,I.LocalEnd,I.StartUtc,I.EndUtc,I.TimeZoneId,I.PolicyVersion,I.BreakMinutes,I.RequiredCount,I.CreatedUtc,I.CreatedBy,I.CreateReason FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) COLLATE Latin1_General_100_BIN2)
 THROW 51121,N'Takvim niyeti dondurulmuştur; gerekçeli iptal ve yeni kayıt kullanın.',1;
END;
GO
CREATE TRIGGER dbo.ProtectCalendar_ShiftAssignments ON dbo.ShiftAssignments AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.ShiftAssignmentId=D.ShiftAssignmentId WHERE I.ShiftAssignmentId IS NULL) THROW 51120,N'Takvim geçmişi silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.ShiftAssignmentId=I.ShiftAssignmentId WHERE (D.CancelledUtc IS NOT NULL OR I.CancelledUtc IS NULL OR EXISTS(SELECT 1 FROM dbo.PlannedShifts S WHERE S.ShiftId=D.ShiftId AND S.Status<>N'Draft')) OR
 (SELECT D.ShiftId,D.PersonId,D.EmploymentId,D.PersonnelGroup,D.RosterAssignmentId,D.CreatedUtc,D.CreatedBy FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) COLLATE Latin1_General_100_BIN2<>
 (SELECT I.ShiftId,I.PersonId,I.EmploymentId,I.PersonnelGroup,I.RosterAssignmentId,I.CreatedUtc,I.CreatedBy FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) COLLATE Latin1_General_100_BIN2)
 THROW 51121,N'Takvim niyeti dondurulmuştur; gerekçeli iptal ve yeni kayıt kullanın.',1;
END;
GO
CREATE TRIGGER dbo.ProtectCalendar_LeaveRequests ON dbo.LeaveRequests AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.LeaveId=D.LeaveId WHERE I.LeaveId IS NULL) THROW 51120,N'Takvim geçmişi silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.LeaveId=I.LeaveId WHERE (NOT((D.Status=N'Requested' AND I.Status IN(N'Approved',N'Rejected',N'Cancelled')) OR(D.Status=N'Approved' AND I.Status=N'Cancelled'))) OR
 (SELECT D.ProjectId,D.PersonId,D.EmploymentId,D.PersonnelGroup,D.PersonVersion,D.RosterAssignmentId,D.LocalStart,D.LocalEnd,D.StartUtc,D.EndUtc,D.TimeZoneId,D.PolicyVersion,D.CreatedUtc,D.CreatedBy,D.CreateReason FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) COLLATE Latin1_General_100_BIN2<>
 (SELECT I.ProjectId,I.PersonId,I.EmploymentId,I.PersonnelGroup,I.PersonVersion,I.RosterAssignmentId,I.LocalStart,I.LocalEnd,I.StartUtc,I.EndUtc,I.TimeZoneId,I.PolicyVersion,I.CreatedUtc,I.CreatedBy,I.CreateReason FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) COLLATE Latin1_General_100_BIN2)
 THROW 51121,N'Takvim niyeti dondurulmuştur; gerekçeli iptal ve yeni kayıt kullanın.',1;
END;
GO
CREATE TRIGGER dbo.ProtectCalendarDecisions ON dbo.PlannedShifts AFTER UPDATE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.ShiftId=I.ShiftId WHERE D.Status=N'Published' AND
 (I.PublishedUtc IS NULL OR I.PublishedUtc<>D.PublishedUtc OR I.PublishedBy IS NULL OR I.PublishedBy COLLATE Latin1_General_100_BIN2<>D.PublishedBy COLLATE Latin1_General_100_BIN2)) THROW 51122,N'Yayın bilgisi değiştirilemez.',1;
END;
GO
CREATE TRIGGER dbo.ProtectCalendarLeaveDecision ON dbo.LeaveRequests AFTER UPDATE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.LeaveId=I.LeaveId WHERE D.Status=N'Approved' AND
 (I.DecidedUtc IS NULL OR I.DecidedUtc<>D.DecidedUtc OR I.DecidedBy IS NULL OR I.DecidedBy COLLATE Latin1_General_100_BIN2<>D.DecidedBy COLLATE Latin1_General_100_BIN2 OR I.DecisionReason IS NULL OR I.DecisionReason COLLATE Latin1_General_100_BIN2<>D.DecisionReason COLLATE Latin1_General_100_BIN2)) THROW 51122,N'İzin onay geçmişi değiştirilemez.',1;
END;
GO
CREATE TRIGGER dbo.AuditCalendar_ShiftTemplates ON dbo.ShiftTemplates AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'Calendar.Change',N'ShiftTemplates',CONVERT(nvarchar(100),COALESCE(I.TemplateId,D.TemplateId)),NULL,CASE WHEN D.TemplateId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',
 CASE WHEN D.TemplateId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 CASE WHEN I.TemplateId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application'
 FROM inserted I FULL OUTER JOIN deleted D ON D.TemplateId=I.TemplateId;
END;
GO
CREATE TRIGGER dbo.AuditCalendar_PlannedShifts ON dbo.PlannedShifts AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'Calendar.Change',N'PlannedShifts',CONVERT(nvarchar(100),COALESCE(I.ShiftId,D.ShiftId)),NULL,CASE WHEN D.ShiftId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',
 CASE WHEN D.ShiftId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 CASE WHEN I.ShiftId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application'
 FROM inserted I FULL OUTER JOIN deleted D ON D.ShiftId=I.ShiftId;
END;
GO
CREATE TRIGGER dbo.AuditCalendar_ShiftAssignments ON dbo.ShiftAssignments AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'Calendar.Change',N'ShiftAssignments',CONVERT(nvarchar(100),COALESCE(I.ShiftAssignmentId,D.ShiftAssignmentId)),COALESCE(I.PersonId,D.PersonId),CASE WHEN D.ShiftAssignmentId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',
 CASE WHEN D.ShiftAssignmentId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 CASE WHEN I.ShiftAssignmentId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application'
 FROM inserted I FULL OUTER JOIN deleted D ON D.ShiftAssignmentId=I.ShiftAssignmentId;
END;
GO
CREATE TRIGGER dbo.AuditCalendar_LeaveRequests ON dbo.LeaveRequests AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'Calendar.Change',N'LeaveRequests',CONVERT(nvarchar(100),COALESCE(I.LeaveId,D.LeaveId)),COALESCE(I.PersonId,D.PersonId),CASE WHEN D.LeaveId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',
 CASE WHEN D.LeaveId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 CASE WHEN I.LeaveId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application'
 FROM inserted I FULL OUTER JOIN deleted D ON D.LeaveId=I.LeaveId;
END;
GO
CREATE OR ALTER FUNCTION dbo.AuditSafeJson(@json nvarchar(max)) RETURNS nvarchar(max) AS
BEGIN
 IF @json IS NULL RETURN NULL;
 IF ISJSON(@json)<>1 OR LEFT(LTRIM(@json),1)<>N'{' RETURN N'{"_privacy":"MASKELENDİ"}';
 DECLARE @body nvarchar(max);
 SELECT @body=STRING_AGG(CONVERT(nvarchar(max),N'"'+STRING_ESCAPE([key],'json')+N'":'+
 CASE WHEN [type]=0 THEN N'null'
 WHEN [key] COLLATE Latin1_General_100_BIN2 IN (N'PersonId',N'EmploymentId',N'CurrentEmploymentId',N'PersonnelGroup',N'IzinId',N'LedgerId',N'EntryId',N'ProjectId',N'TeamId',N'AssignmentId',N'GrantId',N'AccountId',N'FinanceEntryId',N'RequestId',N'ReversesLedgerId',N'RecordVersion',N'RoleName',N'IsActive',N'IsLegacy',N'IsClosed',N'IsCurrent',N'MustChangePassword',N'PasswordResetExpiresUtc',N'CanPay',N'CanExportPersonalData',N'CanViewSalary',N'CanViewIban',N'SourceShiftId',N'SourceShiftVersion',N'CopyBatchId',N'CanPlanShifts',N'CanApproveLeaves',N'ShiftId',N'TemplateId',N'LeaveId',N'StartUtc',N'EndUtc',N'LocalStart',N'LocalEnd',N'Status',N'BreakMinutes',N'RequiredCount',N'StartMinute',N'DurationMinutes',N'PolicyVersion',N'CanPreparePayments',N'CanApprovePayments',N'CredentialChanged',N'IsResponsible',N'CanManageAssignments',N'CanViewCosts',N'CanExport',N'StartDate',N'EndDate',N'PeriodStart',N'PeriodEnd',N'IZINBASLANGIC',N'IZINBITIS',N'CreatedUtc',N'ArchivedUtc',N'ReversedUtc',N'CancelledUtc',N'RevokedUtc',N'BusinessDate',N'TimeZoneId',N'RangeStartUtc',N'RangeEndUtc',N'ClosedUtc',N'PaymentCount',N'ReversalCount',N'CashOut',N'CashReturn',N'NetCash',N'DeclaredCash',N'Difference',N'SnapshotHash',N'Amount',N'PARA',N'Delta',N'Balance',N'Kind',N'PaymentPurpose',N'Rows',N'Format') AND [type] IN(1,2,3)
 THEN CASE WHEN [type]=1 THEN N'"'+STRING_ESCAPE(value,'json')+N'"' ELSE value END
 ELSE N'"[MASKELENDİ]"' END),N',') FROM OPENJSON(@json);
 RETURN N'{'+COALESCE(@body,N'')+N'}';
END;
GO
UPDATE dbo.admin SET SessionEpoch=NEWID();
