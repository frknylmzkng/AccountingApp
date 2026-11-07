-- No identity is inferred from names, TC, creator text or existing project grants.
CREATE TABLE dbo.AccountPersonLinks(
 LinkId uniqueidentifier NOT NULL PRIMARY KEY,
 AccountId uniqueidentifier NOT NULL, -- No FK to deletable accounts; historical AccountId is never reused.
 PersonId uniqueidentifier NOT NULL REFERENCES dbo.PersonnelIdentities(PersonId),
 CreatedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),CreatedByAccountId uniqueidentifier NOT NULL,CreatedBy nvarchar(50) NOT NULL,CreateReason nvarchar(500) NOT NULL,
 RevokedUtc datetime2(7) NULL,RevokedByAccountId uniqueidentifier NULL,RevokedBy nvarchar(50) NULL,RevokeReason nvarchar(500) NULL,RecordVersion rowversion NOT NULL,
 CONSTRAINT CK_AccountPersonRevocation CHECK((RevokedUtc IS NULL AND RevokedByAccountId IS NULL AND RevokedBy IS NULL AND RevokeReason IS NULL) OR (RevokedUtc IS NOT NULL AND RevokedByAccountId IS NOT NULL AND RevokedBy IS NOT NULL AND RevokeReason IS NOT NULL))
);
CREATE UNIQUE INDEX UX_AccountPersonCurrent ON dbo.AccountPersonLinks(AccountId) WHERE RevokedUtc IS NULL;
CREATE INDEX IX_AccountPersonEvidence ON dbo.AccountPersonLinks(AccountId,PersonId);
CREATE INDEX IX_PersonAccountEvidence ON dbo.AccountPersonLinks(PersonId,AccountId);
CREATE TABLE dbo.AccountPersonCommands(
 CommandId uniqueidentifier NOT NULL PRIMARY KEY,ActorId uniqueidentifier NOT NULL,PayloadHash binary(32) NOT NULL,CreatedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
CREATE TRIGGER dbo.GuardAccountPersonCommands ON dbo.AccountPersonCommands AFTER UPDATE,DELETE AS
BEGIN THROW 51200,N'Hesap eşleme işlem kaydı değiştirilemez veya silinemez.',1; END;
GO
CREATE TRIGGER dbo.GuardAccountPersonLinks ON dbo.AccountPersonLinks AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.LinkId=D.LinkId WHERE I.LinkId IS NULL) THROW 51201,N'Hesap/personel eşleme geçmişi silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I LEFT JOIN deleted D ON D.LinkId=I.LinkId WHERE D.LinkId IS NULL AND (I.RevokedUtc IS NOT NULL OR NOT EXISTS(SELECT 1 FROM dbo.admin A WHERE A.AccountId=I.AccountId))) THROW 51202,N'Eşleme mevcut hesaba etkin olarak eklenmeli.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.LinkId=I.LinkId WHERE D.RevokedUtc IS NOT NULL OR I.RevokedUtc IS NULL OR EXISTS(
 SELECT I.AccountId,I.PersonId,I.CreatedUtc,I.CreatedByAccountId,I.CreatedBy COLLATE Latin1_General_100_BIN2,I.CreateReason COLLATE Latin1_General_100_BIN2
 EXCEPT SELECT D.AccountId,D.PersonId,D.CreatedUtc,D.CreatedByAccountId,D.CreatedBy COLLATE Latin1_General_100_BIN2,D.CreateReason COLLATE Latin1_General_100_BIN2)) THROW 51203,N'Eşleme gövdesi değişmez; yalnız gerekçeyle kaldırılabilir.',1;
END;
GO
-- Revocation removes the current association, never the evidence used for segregation of duties.
CREATE FUNCTION dbo.AccountRepresentsPerson(@account uniqueidentifier,@person uniqueidentifier) RETURNS bit AS
BEGIN
 RETURN CASE WHEN EXISTS(SELECT 1 FROM dbo.AccountPersonLinks WHERE AccountId=@account AND PersonId=@person) THEN 1 ELSE 0 END;
END;
GO
CREATE FUNCTION dbo.SameAccountPerson(@first uniqueidentifier,@second uniqueidentifier) RETURNS bit AS
BEGIN
 RETURN CASE WHEN @first=@second OR EXISTS(SELECT 1 FROM dbo.AccountPersonLinks A JOIN dbo.AccountPersonLinks B ON B.PersonId=A.PersonId WHERE A.AccountId=@first AND B.AccountId=@second) THEN 1 ELSE 0 END;
END;
GO
CREATE FUNCTION dbo.TimesheetIdentityConflict(@timesheet uniqueidentifier,@actor uniqueidentifier) RETURNS bit AS
BEGIN
 RETURN CASE WHEN EXISTS(SELECT 1 FROM dbo.Timesheets T WHERE T.TimesheetId=@timesheet AND (
 dbo.AccountRepresentsPerson(@actor,T.PersonId)=1
 OR dbo.SameAccountPerson(@actor,T.CreatedByAccountId)=1 OR dbo.SameAccountPerson(@actor,T.EditedByAccountId)=1 OR dbo.SameAccountPerson(@actor,T.SubmittedByAccountId)=1
 OR EXISTS(SELECT 1 FROM dbo.TimesheetAllocationSets H WHERE H.TimesheetId=T.TimesheetId AND H.ReleasedUtc IS NULL AND (dbo.SameAccountPerson(@actor,H.CreatedByAccountId)=1 OR EXISTS(SELECT 1 FROM dbo.TimesheetApprovalRounds R WHERE R.SetId=H.SetId AND R.ClosedUtc IS NULL AND dbo.SameAccountPerson(@actor,R.SubmittedByAccountId)=1)))
 OR EXISTS(SELECT 1 FROM dbo.AttendanceMatches M JOIN dbo.AttendanceEvents A ON A.EventId=M.InEventId JOIN dbo.AttendanceEvents B ON B.EventId=M.OutEventId WHERE M.TimesheetId=T.TimesheetId AND M.ReleasedUtc IS NULL AND (dbo.SameAccountPerson(@actor,M.MatchedByAccountId)=1 OR dbo.SameAccountPerson(@actor,A.ImportedByAccountId)=1 OR dbo.SameAccountPerson(@actor,B.ImportedByAccountId)=1)))) THEN 1 ELSE 0 END;
END;
GO
CREATE TRIGGER dbo.GuardTimesheetPersonApproval ON dbo.Timesheets AFTER UPDATE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.TimesheetId=I.TimesheetId WHERE I.Status=N'Approved' AND D.Status<>N'Approved' AND dbo.TimesheetIdentityConflict(I.TimesheetId,I.DecidedByAccountId)=1) THROW 51204,N'Çalışan veya aynı kişiye eşlenmiş hazırlayan hesap kendi puantajını onaylayamaz.',1;
END;
GO
CREATE TRIGGER dbo.GuardSegmentPersonApproval ON dbo.TimesheetSegmentApprovals AFTER INSERT,UPDATE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted I JOIN dbo.TimesheetSegments S ON S.SegmentId=I.SegmentId JOIN dbo.TimesheetAllocationSets H ON H.SetId=S.SetId WHERE I.State=N'Approved' AND dbo.TimesheetIdentityConflict(H.TimesheetId,I.DecidedByAccountId)=1) THROW 51205,N'Çalışan veya aynı kişiye eşlenmiş hazırlayan hesap kendi parçasını onaylayamaz.',1;
END;
GO
CREATE TRIGGER dbo.Audit_AccountPersonLinks ON dbo.AccountPersonLinks AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'AccountPerson.Change',N'AccountPersonLinks',CONVERT(nvarchar(100),COALESCE(I.LinkId,D.LinkId)),COALESCE(I.PersonId,D.PersonId),CASE WHEN D.LinkId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),CASE WHEN SESSION_CONTEXT(N'Audit.Reason') IS NULL THEN N'NotProvided' ELSE N'UserProvided' END,
 CASE WHEN D.LinkId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 CASE WHEN I.LinkId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),CASE WHEN SESSION_CONTEXT(N'Audit.Actor') IS NULL THEN N'DirectSql' ELSE N'Application' END
 FROM inserted I FULL OUTER JOIN deleted D ON D.LinkId=I.LinkId;
END;
GO
CREATE OR ALTER FUNCTION dbo.AuditSafeJson(@json nvarchar(max)) RETURNS nvarchar(max) AS
BEGIN
 IF @json IS NULL RETURN NULL;
 IF ISJSON(@json)<>1 OR LEFT(LTRIM(@json),1)<>N'{' RETURN N'{"_privacy":"MASKELENDİ"}';
 DECLARE @body nvarchar(max);
 SELECT @body=STRING_AGG(CONVERT(nvarchar(max),N'"'+STRING_ESCAPE([key],'json')+N'":'+
 CASE WHEN [type]=0 THEN N'null'
 WHEN [key] COLLATE Latin1_General_100_BIN2 IN (N'LinkId',N'RevokedByAccountId',N'EventId',N'MatchId',N'InEventId',N'OutEventId',N'RosterAssignmentId',N'Direction',N'OccurredLocal',N'OccurredUtc',N'ImportedByAccountId',N'MatchedByAccountId',N'ReleasedByAccountId',N'IgnoredByAccountId',N'ImportedUtc',N'IgnoredUtc',N'ReleasedUtc',N'MatchPolicy',N'ProjectVersion',N'RosterVersion',N'SetId',N'SegmentId',N'SourceProjectId',N'SourceVersion',N'SequenceNo',N'NetMinutes',N'GrossMinutes',N'IsSealed',N'IsSupplementary',N'RoundId',N'ApprovalId',N'UsesSegmentApproval',N'LegacyReturn',N'IsReady',N'ClosedByAccountId',N'State',N'PersonId',N'EmploymentId',N'CurrentEmploymentId',N'PersonnelGroup',N'IzinId',N'LedgerId',N'EntryId',N'ProjectId',N'TeamId',N'AssignmentId',N'GrantId',N'AccountId',N'FinanceEntryId',N'RequestId',N'ReversesLedgerId',N'RecordVersion',N'RoleName',N'IsActive',N'IsLegacy',N'IsClosed',N'IsCurrent',N'MustChangePassword',N'PasswordResetExpiresUtc',N'CanPay',N'CanExportPersonalData',N'CanViewSalary',N'CanViewIban',N'SourceShiftId',N'SourceShiftVersion',N'CopyBatchId',N'CanEnterTimesheets',N'CanApproveTimesheets',N'TimesheetId',N'ShiftAssignmentId',N'WorkDate',N'ActualMinutes',N'PayableMinutes',N'CreatedByAccountId',N'EditedByAccountId',N'SubmittedByAccountId',N'DecidedByAccountId',N'EditedUtc',N'SubmittedUtc',N'CanPlanShifts',N'CanApproveLeaves',N'ShiftId',N'TemplateId',N'LeaveId',N'StartUtc',N'EndUtc',N'LocalStart',N'LocalEnd',N'Status',N'BreakMinutes',N'RequiredCount',N'StartMinute',N'DurationMinutes',N'PolicyVersion',N'CanPreparePayments',N'CanApprovePayments',N'CredentialChanged',N'IsResponsible',N'CanManageAssignments',N'CanViewCosts',N'CanExport',N'StartDate',N'EndDate',N'PeriodStart',N'PeriodEnd',N'IZINBASLANGIC',N'IZINBITIS',N'CreatedUtc',N'ArchivedUtc',N'ReversedUtc',N'CancelledUtc',N'RevokedUtc',N'BusinessDate',N'TimeZoneId',N'RangeStartUtc',N'RangeEndUtc',N'ClosedUtc',N'PaymentCount',N'ReversalCount',N'CashOut',N'CashReturn',N'NetCash',N'DeclaredCash',N'Difference',N'SnapshotHash',N'Amount',N'PARA',N'Delta',N'Balance',N'Kind',N'PaymentPurpose',N'Rows',N'Format') AND [type] IN(1,2,3)
 THEN CASE WHEN [type]=1 THEN N'"'+STRING_ESCAPE(value,'json')+N'"' ELSE value END
 ELSE N'"[MASKELENDİ]"' END),N',') FROM OPENJSON(@json);
 RETURN N'{'+COALESCE(@body,N'')+N'}';
END;
GO
UPDATE dbo.admin SET SessionEpoch=NEWID();
