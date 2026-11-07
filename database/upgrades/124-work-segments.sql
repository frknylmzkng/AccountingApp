ALTER TABLE dbo.PersonnelProjectAssignments ADD IsSupplementary bit NOT NULL CONSTRAINT DF_AssignmentSupplementary DEFAULT 0;
GO
CREATE TRIGGER dbo.GuardAssignmentKind ON dbo.PersonnelProjectAssignments AFTER UPDATE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.AssignmentId=I.AssignmentId WHERE I.IsSupplementary<>D.IsSupplementary) THROW 51160,N'Atama türü değiştirilemez; iptal ve yeni atama kullanın.',1;
END;
GO
CREATE TABLE dbo.TimesheetAllocationSets(
 SetId uniqueidentifier NOT NULL PRIMARY KEY,TimesheetId uniqueidentifier NOT NULL REFERENCES dbo.Timesheets(TimesheetId),
 SourceProjectId uniqueidentifier NOT NULL REFERENCES dbo.FinanceProjects(ProjectId),SourceVersion binary(8) NOT NULL,
 LocalStart datetime2(0) NOT NULL,LocalEnd datetime2(0) NOT NULL,StartUtc datetime2(0) NOT NULL,EndUtc datetime2(0) NOT NULL,
 BreakMinutes int NOT NULL,PayableMinutes int NOT NULL,TimeZoneId nvarchar(100) NOT NULL,
 PolicyVersion nvarchar(50) NOT NULL DEFAULT N'ExplicitSegments.v1',IsSealed bit NOT NULL DEFAULT 0,
 CreatedByAccountId uniqueidentifier NOT NULL,CreatedBy nvarchar(50) NOT NULL,CreatedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
 ReleasedUtc datetime2(7) NULL,ReleasedByAccountId uniqueidentifier NULL,ReleasedBy nvarchar(50) NULL,RecordVersion rowversion NOT NULL,
 CONSTRAINT CK_AllocationRange CHECK(StartUtc<EndUtc AND LocalStart<LocalEnd AND BreakMinutes>=0 AND PayableMinutes>=0),
 CONSTRAINT CK_AllocationPolicy CHECK(PolicyVersion=N'ExplicitSegments.v1'),
 CONSTRAINT CK_AllocationRelease CHECK((ReleasedUtc IS NULL AND ReleasedByAccountId IS NULL AND ReleasedBy IS NULL) OR (ReleasedUtc IS NOT NULL AND ReleasedByAccountId IS NOT NULL AND ReleasedBy IS NOT NULL))
);
CREATE UNIQUE INDEX UX_AllocationActive ON dbo.TimesheetAllocationSets(TimesheetId) WHERE ReleasedUtc IS NULL;
CREATE TABLE dbo.TimesheetSegments(
 SegmentId uniqueidentifier NOT NULL PRIMARY KEY,SetId uniqueidentifier NOT NULL REFERENCES dbo.TimesheetAllocationSets(SetId),SequenceNo int NOT NULL,
 ProjectId uniqueidentifier NOT NULL REFERENCES dbo.FinanceProjects(ProjectId),RosterAssignmentId uniqueidentifier NOT NULL REFERENCES dbo.PersonnelProjectAssignments(AssignmentId),ProjectVersion binary(8) NOT NULL,RosterVersion binary(8) NOT NULL,
 LocalStart datetime2(0) NOT NULL,LocalEnd datetime2(0) NOT NULL,StartUtc datetime2(0) NOT NULL,EndUtc datetime2(0) NOT NULL,
 BreakMinutes int NOT NULL,PayableMinutes int NOT NULL,
 GrossMinutes AS DATEDIFF(minute,StartUtc,EndUtc) PERSISTED,
 NetMinutes AS(DATEDIFF(minute,StartUtc,EndUtc)-BreakMinutes) PERSISTED,
 CONSTRAINT UQ_SegmentSequence UNIQUE(SetId,SequenceNo),
 CONSTRAINT CK_SegmentMinutes CHECK(StartUtc<EndUtc AND LocalStart<LocalEnd AND BreakMinutes>=0 AND BreakMinutes<=DATEDIFF(minute,StartUtc,EndUtc) AND PayableMinutes>=0 AND PayableMinutes<=DATEDIFF(minute,StartUtc,EndUtc)-BreakMinutes AND SequenceNo BETWEEN 1 AND 50),
 CONSTRAINT CK_SegmentWholeMinute CHECK(DATEPART(second,LocalStart)=0 AND DATEPART(second,LocalEnd)=0 AND DATEPART(second,StartUtc)=0 AND DATEPART(second,EndUtc)=0)
);
CREATE INDEX IX_SegmentProject ON dbo.TimesheetSegments(ProjectId,StartUtc,SetId) INCLUDE(RosterAssignmentId,NetMinutes,PayableMinutes);
GO
CREATE TRIGGER dbo.GuardTimesheetSegments ON dbo.TimesheetSegments AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51161,N'Zaman parçaları değişmez; dağılımı kaldırıp yeni sürüm oluşturun.',1;
 IF EXISTS(SELECT 1 FROM inserted S JOIN dbo.TimesheetAllocationSets H ON H.SetId=S.SetId JOIN dbo.Timesheets T ON T.TimesheetId=H.TimesheetId
 JOIN dbo.PersonnelProjectAssignments A ON A.AssignmentId=S.RosterAssignmentId
 WHERE H.IsSealed=1 OR H.ReleasedUtc IS NOT NULL OR T.Status NOT IN(N'Draft',N'Returned') OR S.StartUtc<H.StartUtc OR S.EndUtc>H.EndUtc OR S.LocalStart<H.LocalStart OR S.LocalEnd>H.LocalEnd
 OR A.ProjectId<>S.ProjectId OR A.PersonId<>T.PersonId OR A.PersonnelGroup<>T.PersonnelGroup OR A.CancelledUtc IS NOT NULL
 OR NOT EXISTS(SELECT 1 FROM dbo.EmploymentRecordLinks L WHERE L.RecordKind=N'Assignment' AND L.RecordKey=CONVERT(nvarchar(64),A.AssignmentId) AND L.EmploymentId=T.EmploymentId)) THROW 51162,N'Zaman parçası kaynak puantaj/dönem/atama ile uyuşmuyor.',1;
END;
GO
CREATE TRIGGER dbo.GuardAllocationSets ON dbo.TimesheetAllocationSets AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.SetId=D.SetId WHERE I.SetId IS NULL) THROW 51163,N'Dağılım geçmişi silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I LEFT JOIN deleted D ON D.SetId=I.SetId WHERE D.SetId IS NULL AND (I.IsSealed<>0 OR I.ReleasedUtc IS NOT NULL)) THROW 51164,N'Dağılım taslak başlamalı.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.SetId=I.SetId WHERE D.ReleasedUtc IS NOT NULL OR NOT((D.IsSealed=0 AND I.IsSealed=1 AND I.ReleasedUtc IS NULL) OR (I.IsSealed=D.IsSealed AND I.ReleasedUtc IS NOT NULL)) OR EXISTS(
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
CREATE TRIGGER dbo.GuardAllocatedTimesheet ON dbo.Timesheets AFTER UPDATE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted T JOIN dbo.TimesheetAllocationSets H ON H.TimesheetId=T.TimesheetId AND H.ReleasedUtc IS NULL WHERE T.Status=N'Cancelled' OR (T.Status IN(N'Submitted',N'Approved') AND H.IsSealed=0) OR EXISTS(
 SELECT H.LocalStart,H.LocalEnd,H.StartUtc,H.EndUtc,H.BreakMinutes,H.PayableMinutes,H.TimeZoneId
 EXCEPT SELECT T.LocalStart,T.LocalEnd,T.StartUtc,T.EndUtc,T.BreakMinutes,T.PayableMinutes,T.TimeZoneId)) THROW 51168,N'Dağıtılmış puantajın gövdesi değişmez; önce dağılımı kaldırın.',1;
END;
GO
CREATE TRIGGER dbo.Audit_TimesheetAllocationSets ON dbo.TimesheetAllocationSets AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'Allocation.Change',N'TimesheetAllocationSets',CONVERT(nvarchar(100),COALESCE(I.SetId,D.SetId)),(SELECT T.PersonId FROM dbo.Timesheets T WHERE T.TimesheetId=COALESCE(I.TimesheetId,D.TimesheetId)),CASE WHEN D.SetId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',CASE WHEN D.SetId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 CASE WHEN I.SetId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application'
 FROM inserted I FULL OUTER JOIN deleted D ON D.SetId=I.SetId;
END;
GO
CREATE TRIGGER dbo.Audit_TimesheetSegments ON dbo.TimesheetSegments AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'Allocation.Change',N'TimesheetSegments',CONVERT(nvarchar(100),COALESCE(I.SegmentId,D.SegmentId)),(SELECT T.PersonId FROM dbo.TimesheetAllocationSets H JOIN dbo.Timesheets T ON T.TimesheetId=H.TimesheetId WHERE H.SetId=COALESCE(I.SetId,D.SetId)),CASE WHEN D.SegmentId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',CASE WHEN D.SegmentId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 CASE WHEN I.SegmentId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application'
 FROM inserted I FULL OUTER JOIN deleted D ON D.SegmentId=I.SegmentId;
END;
GO
CREATE OR ALTER FUNCTION dbo.AuditSafeJson(@json nvarchar(max)) RETURNS nvarchar(max) AS
BEGIN
 IF @json IS NULL RETURN NULL;
 IF ISJSON(@json)<>1 OR LEFT(LTRIM(@json),1)<>N'{' RETURN N'{"_privacy":"MASKELENDİ"}';
 DECLARE @body nvarchar(max);
 SELECT @body=STRING_AGG(CONVERT(nvarchar(max),N'"'+STRING_ESCAPE([key],'json')+N'":'+
 CASE WHEN [type]=0 THEN N'null'
 WHEN [key] COLLATE Latin1_General_100_BIN2 IN (N'EventId',N'MatchId',N'InEventId',N'OutEventId',N'RosterAssignmentId',N'Direction',N'OccurredLocal',N'OccurredUtc',N'ImportedByAccountId',N'MatchedByAccountId',N'ReleasedByAccountId',N'IgnoredByAccountId',N'ImportedUtc',N'IgnoredUtc',N'ReleasedUtc',N'MatchPolicy',N'ProjectVersion',N'RosterVersion',N'SetId',N'SegmentId',N'SourceProjectId',N'SourceVersion',N'SequenceNo',N'NetMinutes',N'GrossMinutes',N'IsSealed',N'IsSupplementary',N'PersonId',N'EmploymentId',N'CurrentEmploymentId',N'PersonnelGroup',N'IzinId',N'LedgerId',N'EntryId',N'ProjectId',N'TeamId',N'AssignmentId',N'GrantId',N'AccountId',N'FinanceEntryId',N'RequestId',N'ReversesLedgerId',N'RecordVersion',N'RoleName',N'IsActive',N'IsLegacy',N'IsClosed',N'IsCurrent',N'MustChangePassword',N'PasswordResetExpiresUtc',N'CanPay',N'CanExportPersonalData',N'CanViewSalary',N'CanViewIban',N'SourceShiftId',N'SourceShiftVersion',N'CopyBatchId',N'CanEnterTimesheets',N'CanApproveTimesheets',N'TimesheetId',N'ShiftAssignmentId',N'WorkDate',N'ActualMinutes',N'PayableMinutes',N'CreatedByAccountId',N'EditedByAccountId',N'SubmittedByAccountId',N'DecidedByAccountId',N'EditedUtc',N'SubmittedUtc',N'CanPlanShifts',N'CanApproveLeaves',N'ShiftId',N'TemplateId',N'LeaveId',N'StartUtc',N'EndUtc',N'LocalStart',N'LocalEnd',N'Status',N'BreakMinutes',N'RequiredCount',N'StartMinute',N'DurationMinutes',N'PolicyVersion',N'CanPreparePayments',N'CanApprovePayments',N'CredentialChanged',N'IsResponsible',N'CanManageAssignments',N'CanViewCosts',N'CanExport',N'StartDate',N'EndDate',N'PeriodStart',N'PeriodEnd',N'IZINBASLANGIC',N'IZINBITIS',N'CreatedUtc',N'ArchivedUtc',N'ReversedUtc',N'CancelledUtc',N'RevokedUtc',N'BusinessDate',N'TimeZoneId',N'RangeStartUtc',N'RangeEndUtc',N'ClosedUtc',N'PaymentCount',N'ReversalCount',N'CashOut',N'CashReturn',N'NetCash',N'DeclaredCash',N'Difference',N'SnapshotHash',N'Amount',N'PARA',N'Delta',N'Balance',N'Kind',N'PaymentPurpose',N'Rows',N'Format') AND [type] IN(1,2,3)
 THEN CASE WHEN [type]=1 THEN N'"'+STRING_ESCAPE(value,'json')+N'"' ELSE value END
 ELSE N'"[MASKELENDİ]"' END),N',') FROM OPENJSON(@json);
 RETURN N'{'+COALESCE(@body,N'')+N'}';
END;
GO
UPDATE dbo.admin SET SessionEpoch=NEWID();
