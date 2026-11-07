ALTER TABLE dbo.TimesheetAllocationSets ADD UsesSegmentApproval bit NOT NULL CONSTRAINT DF_AllocationSegmentApproval DEFAULT 0;
GO
CREATE OR ALTER TRIGGER dbo.GuardAllocationSets ON dbo.TimesheetAllocationSets AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.SetId=D.SetId WHERE I.SetId IS NULL) THROW 51163,N'Dağılım geçmişi silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I LEFT JOIN deleted D ON D.SetId=I.SetId WHERE D.SetId IS NULL AND (I.IsSealed<>0 OR I.ReleasedUtc IS NOT NULL)) THROW 51164,N'Dağılım taslak başlamalı.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.SetId=I.SetId WHERE D.ReleasedUtc IS NOT NULL OR NOT((D.IsSealed=0 AND I.IsSealed=1 AND I.ReleasedUtc IS NULL) OR (D.IsSealed=1 AND I.IsSealed=1 AND D.UsesSegmentApproval=0 AND I.UsesSegmentApproval=1 AND I.ReleasedUtc IS NULL) OR (I.IsSealed=D.IsSealed AND I.ReleasedUtc IS NOT NULL)) OR EXISTS(
 SELECT I.TimesheetId,I.SourceProjectId,I.SourceVersion,I.LocalStart,I.LocalEnd,I.StartUtc,I.EndUtc,I.BreakMinutes,I.PayableMinutes,I.TimeZoneId COLLATE Latin1_General_100_BIN2,I.PolicyVersion COLLATE Latin1_General_100_BIN2,I.CreatedByAccountId,I.CreatedBy COLLATE Latin1_General_100_BIN2,I.CreatedUtc
 EXCEPT SELECT D.TimesheetId,D.SourceProjectId,D.SourceVersion,D.LocalStart,D.LocalEnd,D.StartUtc,D.EndUtc,D.BreakMinutes,D.PayableMinutes,D.TimeZoneId COLLATE Latin1_General_100_BIN2,D.PolicyVersion COLLATE Latin1_General_100_BIN2,D.CreatedByAccountId,D.CreatedBy COLLATE Latin1_General_100_BIN2,D.CreatedUtc)) THROW 51165,N'Dağılım gövdesi değişmez; yalnız tamamlanabilir/kaldırılabilir.',1;
 IF EXISTS(SELECT 1 FROM inserted H JOIN dbo.Timesheets T ON T.TimesheetId=H.TimesheetId WHERE T.Status NOT IN(N'Draft',N'Returned') OR T.ProjectId<>H.SourceProjectId OR EXISTS(
 SELECT H.LocalStart,H.LocalEnd,H.StartUtc,H.EndUtc,H.BreakMinutes,H.PayableMinutes,H.TimeZoneId
 EXCEPT SELECT T.LocalStart,T.LocalEnd,T.StartUtc,T.EndUtc,T.BreakMinutes,T.PayableMinutes,T.TimeZoneId)) THROW 51166,N'Dağılım güncel taslak/iade puantaj gövdesine ait olmalı.',1;
 IF EXISTS(SELECT 1 FROM inserted H WHERE H.IsSealed=1 AND H.ReleasedUtc IS NULL AND (
 (SELECT COUNT(*) FROM dbo.TimesheetSegments S WHERE S.SetId=H.SetId) NOT BETWEEN 1 AND 50
 OR (SELECT COALESCE(SUM(S.GrossMinutes),-1) FROM dbo.TimesheetSegments S WHERE S.SetId=H.SetId)<>DATEDIFF(minute,H.StartUtc,H.EndUtc)
 OR (SELECT COALESCE(SUM(S.BreakMinutes),-1) FROM dbo.TimesheetSegments S WHERE S.SetId=H.SetId)<>H.BreakMinutes
 OR (SELECT COALESCE(SUM(S.PayableMinutes),-1) FROM dbo.TimesheetSegments S WHERE S.SetId=H.SetId)<>H.PayableMinutes
 OR EXISTS(SELECT 1 FROM dbo.TimesheetSegments A JOIN dbo.TimesheetSegments B ON B.SetId=A.SetId AND A.SequenceNo<B.SequenceNo WHERE A.SetId=H.SetId AND A.StartUtc<B.EndUtc AND A.EndUtc>B.StartUtc))) THROW 51167,N'Parçalar boşluksuz/çakışmasız olmalı; mola ve ücret dakikası toplamları puantajla eşleşmeli.',1;
END;
GO
CREATE TABLE dbo.TimesheetApprovalRounds(
 RoundId uniqueidentifier NOT NULL PRIMARY KEY,SetId uniqueidentifier NOT NULL REFERENCES dbo.TimesheetAllocationSets(SetId),SourceVersion binary(8) NOT NULL,
 SubmittedByAccountId uniqueidentifier NOT NULL,SubmittedBy nvarchar(50) NOT NULL,SubmittedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),IsReady bit NOT NULL DEFAULT 0,
 ClosedUtc datetime2(7) NULL,ClosedByAccountId uniqueidentifier NULL,ClosedBy nvarchar(50) NULL,RecordVersion rowversion NOT NULL,
 CONSTRAINT CK_SegmentRoundClose CHECK((ClosedUtc IS NULL AND ClosedByAccountId IS NULL AND ClosedBy IS NULL) OR (ClosedUtc IS NOT NULL AND ClosedByAccountId IS NOT NULL AND ClosedBy IS NOT NULL))
);
CREATE UNIQUE INDEX UX_SegmentRoundActive ON dbo.TimesheetApprovalRounds(SetId) WHERE ClosedUtc IS NULL;
CREATE TABLE dbo.TimesheetSegmentApprovals(
 ApprovalId uniqueidentifier NOT NULL PRIMARY KEY,RoundId uniqueidentifier NULL REFERENCES dbo.TimesheetApprovalRounds(RoundId),
 SegmentId uniqueidentifier NOT NULL REFERENCES dbo.TimesheetSegments(SegmentId),ProjectId uniqueidentifier NOT NULL REFERENCES dbo.FinanceProjects(ProjectId),
 LegacyReturn bit NOT NULL DEFAULT 0,State nvarchar(20) NOT NULL DEFAULT N'Pending',
 DecidedByAccountId uniqueidentifier NULL,DecidedBy nvarchar(50) NULL,DecidedUtc datetime2(7) NULL,DecisionReason nvarchar(500) NULL,RecordVersion rowversion NOT NULL,
 CONSTRAINT CK_SegmentDecisionState CHECK(State IN(N'Pending',N'Approved',N'Returned')),
 CONSTRAINT CK_SegmentDecisionActor CHECK((State=N'Pending' AND DecidedByAccountId IS NULL AND DecidedBy IS NULL AND DecidedUtc IS NULL AND DecisionReason IS NULL) OR (State<>N'Pending' AND DecidedByAccountId IS NOT NULL AND DecidedBy IS NOT NULL AND DecidedUtc IS NOT NULL AND DecisionReason IS NOT NULL)),
 CONSTRAINT CK_SegmentDecisionKind CHECK((LegacyReturn=0 AND RoundId IS NOT NULL) OR (LegacyReturn=1 AND RoundId IS NULL AND State=N'Returned'))
);
CREATE UNIQUE INDEX UX_SegmentDecision ON dbo.TimesheetSegmentApprovals(RoundId,SegmentId) WHERE RoundId IS NOT NULL;
CREATE INDEX IX_SegmentDecisionProject ON dbo.TimesheetSegmentApprovals(ProjectId,RoundId,SegmentId) INCLUDE(State);
CREATE TABLE dbo.TimesheetSegmentDecisionEvents(
 EventId uniqueidentifier NOT NULL PRIMARY KEY,ApprovalId uniqueidentifier NOT NULL REFERENCES dbo.TimesheetSegmentApprovals(ApprovalId),
 RoundId uniqueidentifier NULL,SegmentId uniqueidentifier NOT NULL,ProjectId uniqueidentifier NOT NULL REFERENCES dbo.FinanceProjects(ProjectId),
 LegacyReturn bit NOT NULL,State nvarchar(20) NOT NULL,DecidedByAccountId uniqueidentifier NOT NULL,DecidedBy nvarchar(50) NOT NULL,DecidedUtc datetime2(7) NOT NULL,DecisionReason nvarchar(500) NOT NULL
);
CREATE INDEX IX_SegmentDecisionEventProject ON dbo.TimesheetSegmentDecisionEvents(ProjectId,SegmentId,DecidedUtc);
GO
CREATE TRIGGER dbo.GuardSegmentDecisionEvents ON dbo.TimesheetSegmentDecisionEvents AFTER UPDATE,DELETE AS
BEGIN THROW 51194,N'Parça karar olayları değiştirilemez veya silinemez.',1; END;
GO
CREATE TRIGGER dbo.GuardAllocationApprovalMode ON dbo.TimesheetAllocationSets AFTER UPDATE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.SetId=I.SetId WHERE I.UsesSegmentApproval<>D.UsesSegmentApproval AND NOT(D.UsesSegmentApproval=0 AND I.UsesSegmentApproval=1 AND I.IsSealed=1 AND I.ReleasedUtc IS NULL)) THROW 51180,N'Parça onay modeli geriye çevrilemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN dbo.TimesheetApprovalRounds R ON R.SetId=I.SetId WHERE I.ReleasedUtc IS NOT NULL AND R.ClosedUtc IS NULL) THROW 51181,N'Açık onay turu varken dağılım kaldırılamaz.',1;
END;
GO
CREATE TRIGGER dbo.GuardSegmentApprovalRounds ON dbo.TimesheetApprovalRounds AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.RoundId=D.RoundId WHERE I.RoundId IS NULL) THROW 51182,N'Onay turu silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I LEFT JOIN deleted D ON D.RoundId=I.RoundId JOIN dbo.TimesheetAllocationSets H ON H.SetId=I.SetId JOIN dbo.Timesheets T ON T.TimesheetId=H.TimesheetId WHERE D.RoundId IS NULL AND (I.IsReady<>0 OR I.ClosedUtc IS NOT NULL OR H.UsesSegmentApproval<>1 OR H.IsSealed<>1 OR H.ReleasedUtc IS NOT NULL OR T.Status NOT IN(N'Draft',N'Returned'))) THROW 51183,N'Tur güncel taslak/iade dağılımından başlamalı.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.RoundId=I.RoundId WHERE D.ClosedUtc IS NOT NULL OR NOT((D.IsReady=0 AND I.IsReady=1 AND I.ClosedUtc IS NULL) OR (D.IsReady=1 AND I.IsReady=1 AND I.ClosedUtc IS NOT NULL)) OR EXISTS(
 SELECT I.SetId,I.SourceVersion,I.SubmittedByAccountId,I.SubmittedBy COLLATE Latin1_General_100_BIN2,I.SubmittedUtc
 EXCEPT SELECT D.SetId,D.SourceVersion,D.SubmittedByAccountId,D.SubmittedBy COLLATE Latin1_General_100_BIN2,D.SubmittedUtc)) THROW 51184,N'Tur gövdesi değişmez; yalnız tamamlanır veya iade ile kapanır.',1;
 IF EXISTS(SELECT 1 FROM inserted I WHERE I.IsReady=1 AND I.ClosedUtc IS NULL AND ((SELECT COUNT(*) FROM dbo.TimesheetSegmentApprovals D WHERE D.RoundId=I.RoundId)<>(SELECT COUNT(*) FROM dbo.TimesheetSegments S WHERE S.SetId=I.SetId) OR NOT EXISTS(SELECT 1 FROM dbo.TimesheetSegmentApprovals D WHERE D.RoundId=I.RoundId))) THROW 51185,N'Tur bütün parçaları kapsamalı.',1;
 IF EXISTS(SELECT 1 FROM inserted I WHERE I.ClosedUtc IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.TimesheetSegmentApprovals D WHERE D.RoundId=I.RoundId AND D.State=N'Returned')) THROW 51186,N'Tur ancak parça iadesiyle kapanır.',1;
END;
GO
CREATE TRIGGER dbo.GuardSegmentApprovals ON dbo.TimesheetSegmentApprovals AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.ApprovalId=D.ApprovalId WHERE I.ApprovalId IS NULL) THROW 51187,N'Parça kararı silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.ApprovalId=I.ApprovalId WHERE EXISTS(SELECT I.RoundId,I.SegmentId,I.ProjectId,I.LegacyReturn EXCEPT SELECT D.RoundId,D.SegmentId,D.ProjectId,D.LegacyReturn)) THROW 51195,N'Kararın tur/parça/proje kimliği değişmez.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.ApprovalId=I.ApprovalId WHERE D.LegacyReturn=1) THROW 51193,N'Eski kayıt iade kararı değişmez.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN dbo.TimesheetSegments S ON S.SegmentId=I.SegmentId LEFT JOIN dbo.TimesheetApprovalRounds R ON R.RoundId=I.RoundId WHERE S.ProjectId<>I.ProjectId OR (I.LegacyReturn=0 AND S.SetId<>R.SetId)) THROW 51188,N'Karar parça/proje/tur kapsamına ait değil.',1;
 IF EXISTS(SELECT 1 FROM inserted I LEFT JOIN deleted D ON D.ApprovalId=I.ApprovalId LEFT JOIN dbo.TimesheetApprovalRounds R ON R.RoundId=I.RoundId JOIN dbo.TimesheetSegments S ON S.SegmentId=I.SegmentId JOIN dbo.TimesheetAllocationSets H ON H.SetId=S.SetId JOIN dbo.Timesheets T ON T.TimesheetId=H.TimesheetId WHERE D.ApprovalId IS NULL AND
 ((I.LegacyReturn=0 AND (I.State<>N'Pending' OR R.IsReady<>0 OR R.ClosedUtc IS NOT NULL OR T.Status NOT IN(N'Draft',N'Returned'))) OR (I.LegacyReturn=1 AND (H.UsesSegmentApproval<>0 OR H.ReleasedUtc IS NOT NULL OR T.Status NOT IN(N'Submitted',N'Approved'))))) THROW 51189,N'Yeni karar yalnız bekleyen tur veya eski kayıt iadesi olabilir.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.ApprovalId=I.ApprovalId JOIN dbo.TimesheetApprovalRounds R ON R.RoundId=I.RoundId JOIN dbo.TimesheetAllocationSets H ON H.SetId=R.SetId JOIN dbo.Timesheets T ON T.TimesheetId=H.TimesheetId WHERE D.LegacyReturn=1 OR R.IsReady<>1 OR R.ClosedUtc IS NOT NULL OR H.ReleasedUtc IS NOT NULL OR T.Status NOT IN(N'Submitted',N'Approved') OR NOT((D.State=N'Pending' AND I.State IN(N'Approved',N'Returned')) OR (D.State=N'Approved' AND I.State=N'Returned')) OR EXISTS(
 SELECT I.RoundId,I.SegmentId,I.ProjectId,I.LegacyReturn EXCEPT SELECT D.RoundId,D.SegmentId,D.ProjectId,D.LegacyReturn)) THROW 51190,N'Eski/kapalı tur kararı değiştirilemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN dbo.TimesheetSegments S ON S.SegmentId=I.SegmentId JOIN dbo.TimesheetAllocationSets H ON H.SetId=S.SetId JOIN dbo.Timesheets T ON T.TimesheetId=H.TimesheetId JOIN dbo.TimesheetApprovalRounds R ON R.RoundId=I.RoundId WHERE I.State=N'Approved' AND
 (I.DecidedByAccountId=T.CreatedByAccountId OR I.DecidedByAccountId=T.EditedByAccountId OR I.DecidedByAccountId=R.SubmittedByAccountId OR I.DecidedByAccountId=H.CreatedByAccountId OR EXISTS(SELECT 1 FROM dbo.AttendanceMatches M JOIN dbo.AttendanceEvents A ON A.EventId=M.InEventId JOIN dbo.AttendanceEvents B ON B.EventId=M.OutEventId WHERE M.TimesheetId=T.TimesheetId AND M.ReleasedUtc IS NULL AND (M.MatchedByAccountId=I.DecidedByAccountId OR A.ImportedByAccountId=I.DecidedByAccountId OR B.ImportedByAccountId=I.DecidedByAccountId)))) THROW 51191,N'Hazırlayan/olay katılımcısı kendi parçasını onaylayamaz.',1;
END;
GO
CREATE TRIGGER dbo.GuardSegmentDerivedStatus ON dbo.Timesheets AFTER UPDATE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted T JOIN dbo.TimesheetAllocationSets H ON H.TimesheetId=T.TimesheetId AND H.ReleasedUtc IS NULL AND H.UsesSegmentApproval=1 WHERE
 (T.Status IN(N'Submitted',N'Approved') AND NOT EXISTS(SELECT 1 FROM dbo.TimesheetApprovalRounds R WHERE R.SetId=H.SetId AND R.IsReady=1 AND R.ClosedUtc IS NULL))
 OR (T.Status=N'Approved' AND EXISTS(SELECT 1 FROM dbo.TimesheetApprovalRounds R JOIN dbo.TimesheetSegmentApprovals D ON D.RoundId=R.RoundId WHERE R.SetId=H.SetId AND R.ClosedUtc IS NULL AND D.State<>N'Approved'))
 OR (T.Status=N'Submitted' AND NOT EXISTS(SELECT 1 FROM dbo.TimesheetApprovalRounds R JOIN dbo.TimesheetSegmentApprovals D ON D.RoundId=R.RoundId WHERE R.SetId=H.SetId AND R.ClosedUtc IS NULL AND D.State=N'Pending'))
 OR (T.Status IN(N'Draft',N'Returned') AND EXISTS(SELECT 1 FROM dbo.TimesheetApprovalRounds R WHERE R.SetId=H.SetId AND R.IsReady=1 AND R.ClosedUtc IS NULL))) THROW 51192,N'Ana puantaj durumu parça kararlarından türemeli.',1;
END;
GO
CREATE TRIGGER dbo.Audit_TimesheetApprovalRounds ON dbo.TimesheetApprovalRounds AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'SegmentApproval.Change',N'TimesheetApprovalRounds',CONVERT(nvarchar(100),COALESCE(I.RoundId,D.RoundId)),(SELECT T.PersonId FROM dbo.TimesheetAllocationSets H JOIN dbo.Timesheets T ON T.TimesheetId=H.TimesheetId WHERE H.SetId=COALESCE(I.SetId,D.SetId)),CASE WHEN D.RoundId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',CASE WHEN D.RoundId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 CASE WHEN I.RoundId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application'
 FROM inserted I FULL OUTER JOIN deleted D ON D.RoundId=I.RoundId;
END;
GO
CREATE TRIGGER dbo.Audit_TimesheetSegmentApprovals ON dbo.TimesheetSegmentApprovals AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'SegmentApproval.Change',N'TimesheetSegmentApprovals',CONVERT(nvarchar(100),COALESCE(I.ApprovalId,D.ApprovalId)),(SELECT T.PersonId FROM dbo.TimesheetSegments S JOIN dbo.TimesheetAllocationSets H ON H.SetId=S.SetId JOIN dbo.Timesheets T ON T.TimesheetId=H.TimesheetId WHERE S.SegmentId=COALESCE(I.SegmentId,D.SegmentId)),CASE WHEN D.ApprovalId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',CASE WHEN D.ApprovalId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 CASE WHEN I.ApprovalId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application'
 FROM inserted I FULL OUTER JOIN deleted D ON D.ApprovalId=I.ApprovalId;
 INSERT dbo.TimesheetSegmentDecisionEvents(EventId,ApprovalId,RoundId,SegmentId,ProjectId,LegacyReturn,State,DecidedByAccountId,DecidedBy,DecidedUtc,DecisionReason)
 SELECT NEWID(),ApprovalId,RoundId,SegmentId,ProjectId,LegacyReturn,State,DecidedByAccountId,DecidedBy,DecidedUtc,DecisionReason FROM inserted WHERE State<>N'Pending';
END;
GO
CREATE OR ALTER FUNCTION dbo.AuditSafeJson(@json nvarchar(max)) RETURNS nvarchar(max) AS
BEGIN
 IF @json IS NULL RETURN NULL;
 IF ISJSON(@json)<>1 OR LEFT(LTRIM(@json),1)<>N'{' RETURN N'{"_privacy":"MASKELENDİ"}';
 DECLARE @body nvarchar(max);
 SELECT @body=STRING_AGG(CONVERT(nvarchar(max),N'"'+STRING_ESCAPE([key],'json')+N'":'+
 CASE WHEN [type]=0 THEN N'null'
 WHEN [key] COLLATE Latin1_General_100_BIN2 IN (N'EventId',N'MatchId',N'InEventId',N'OutEventId',N'RosterAssignmentId',N'Direction',N'OccurredLocal',N'OccurredUtc',N'ImportedByAccountId',N'MatchedByAccountId',N'ReleasedByAccountId',N'IgnoredByAccountId',N'ImportedUtc',N'IgnoredUtc',N'ReleasedUtc',N'MatchPolicy',N'ProjectVersion',N'RosterVersion',N'SetId',N'SegmentId',N'SourceProjectId',N'SourceVersion',N'SequenceNo',N'NetMinutes',N'GrossMinutes',N'IsSealed',N'IsSupplementary',N'RoundId',N'ApprovalId',N'UsesSegmentApproval',N'LegacyReturn',N'IsReady',N'ClosedByAccountId',N'State',N'PersonId',N'EmploymentId',N'CurrentEmploymentId',N'PersonnelGroup',N'IzinId',N'LedgerId',N'EntryId',N'ProjectId',N'TeamId',N'AssignmentId',N'GrantId',N'AccountId',N'FinanceEntryId',N'RequestId',N'ReversesLedgerId',N'RecordVersion',N'RoleName',N'IsActive',N'IsLegacy',N'IsClosed',N'IsCurrent',N'MustChangePassword',N'PasswordResetExpiresUtc',N'CanPay',N'CanExportPersonalData',N'CanViewSalary',N'CanViewIban',N'SourceShiftId',N'SourceShiftVersion',N'CopyBatchId',N'CanEnterTimesheets',N'CanApproveTimesheets',N'TimesheetId',N'ShiftAssignmentId',N'WorkDate',N'ActualMinutes',N'PayableMinutes',N'CreatedByAccountId',N'EditedByAccountId',N'SubmittedByAccountId',N'DecidedByAccountId',N'EditedUtc',N'SubmittedUtc',N'CanPlanShifts',N'CanApproveLeaves',N'ShiftId',N'TemplateId',N'LeaveId',N'StartUtc',N'EndUtc',N'LocalStart',N'LocalEnd',N'Status',N'BreakMinutes',N'RequiredCount',N'StartMinute',N'DurationMinutes',N'PolicyVersion',N'CanPreparePayments',N'CanApprovePayments',N'CredentialChanged',N'IsResponsible',N'CanManageAssignments',N'CanViewCosts',N'CanExport',N'StartDate',N'EndDate',N'PeriodStart',N'PeriodEnd',N'IZINBASLANGIC',N'IZINBITIS',N'CreatedUtc',N'ArchivedUtc',N'ReversedUtc',N'CancelledUtc',N'RevokedUtc',N'BusinessDate',N'TimeZoneId',N'RangeStartUtc',N'RangeEndUtc',N'ClosedUtc',N'PaymentCount',N'ReversalCount',N'CashOut',N'CashReturn',N'NetCash',N'DeclaredCash',N'Difference',N'SnapshotHash',N'Amount',N'PARA',N'Delta',N'Balance',N'Kind',N'PaymentPurpose',N'Rows',N'Format') AND [type] IN(1,2,3)
 THEN CASE WHEN [type]=1 THEN N'"'+STRING_ESCAPE(value,'json')+N'"' ELSE value END
 ELSE N'"[MASKELENDİ]"' END),N',') FROM OPENJSON(@json);
 RETURN N'{'+COALESCE(@body,N'')+N'}';
END;
GO
UPDATE dbo.admin SET SessionEpoch=NEWID();
