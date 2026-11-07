-- Calculation foundation only: no balance, FinanceEntries or PaymentLedger posting.
CREATE TABLE dbo.HourlyRateAgreements(
 RateId uniqueidentifier NOT NULL PRIMARY KEY,ProjectId uniqueidentifier NOT NULL REFERENCES dbo.FinanceProjects(ProjectId),
 PersonId uniqueidentifier NOT NULL REFERENCES dbo.PersonnelIdentities(PersonId),EmploymentId uniqueidentifier NOT NULL REFERENCES dbo.EmploymentPeriods(EmploymentId),
 ValidFrom date NOT NULL,ValidTo date NULL,HourlyAmount decimal(18,2) NOT NULL,Currency nvarchar(3) NOT NULL DEFAULT N'TRY',
 CreatedByAccountId uniqueidentifier NOT NULL,CreatedBy nvarchar(50) NOT NULL,CreatedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
 RevokedUtc datetime2(7) NULL,RevokedByAccountId uniqueidentifier NULL,RevokedBy nvarchar(50) NULL,RecordVersion rowversion NOT NULL,
 CONSTRAINT CK_HourlyRate CHECK(HourlyAmount>0 AND HourlyAmount<=1000000 AND Currency=N'TRY' AND (ValidTo IS NULL OR ValidTo>=ValidFrom)),
 CONSTRAINT CK_HourlyRateRevocation CHECK((RevokedUtc IS NULL AND RevokedByAccountId IS NULL AND RevokedBy IS NULL) OR (RevokedUtc IS NOT NULL AND RevokedByAccountId IS NOT NULL AND RevokedBy IS NOT NULL))
);
CREATE INDEX IX_HourlyRateRange ON dbo.HourlyRateAgreements(ProjectId,EmploymentId,ValidFrom,ValidTo) INCLUDE(RevokedUtc,HourlyAmount);
CREATE TABLE dbo.AccrualCalculations(
 CalculationId uniqueidentifier NOT NULL PRIMARY KEY,ProjectId uniqueidentifier NOT NULL REFERENCES dbo.FinanceProjects(ProjectId),PeriodStart date NOT NULL,PeriodEnd date NOT NULL,
 Policy nvarchar(60) NOT NULL,Currency nvarchar(3) NOT NULL,TotalAmount decimal(18,2) NOT NULL,LineCount int NOT NULL,SnapshotHash binary(32) NOT NULL,IsSealed bit NOT NULL DEFAULT 0,
 CreatedByAccountId uniqueidentifier NOT NULL,CreatedBy nvarchar(50) NOT NULL,CreatedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),RecordVersion rowversion NOT NULL,
 CONSTRAINT UX_AccrualCalculationPeriod UNIQUE(ProjectId,PeriodStart,PeriodEnd),
 CONSTRAINT CK_AccrualCalculation CHECK(PeriodEnd>=PeriodStart AND TotalAmount>=0 AND LineCount BETWEEN 1 AND 2000 AND Currency=N'TRY' AND Policy=N'HourlyTRY.WorkDate.PerLineAway.v1')
);
CREATE TABLE dbo.AccrualCalculationLines(
 LineId uniqueidentifier NOT NULL PRIMARY KEY,CalculationId uniqueidentifier NOT NULL REFERENCES dbo.AccrualCalculations(CalculationId),
 SourceKind nvarchar(10) NOT NULL,SourceId uniqueidentifier NOT NULL,TimesheetId uniqueidentifier NOT NULL REFERENCES dbo.Timesheets(TimesheetId),
 PersonId uniqueidentifier NOT NULL REFERENCES dbo.PersonnelIdentities(PersonId),EmploymentId uniqueidentifier NOT NULL REFERENCES dbo.EmploymentPeriods(EmploymentId),PersonCaption nvarchar(101) NOT NULL,WorkDate date NOT NULL,
 PaidMinutes int NOT NULL,RateId uniqueidentifier NOT NULL REFERENCES dbo.HourlyRateAgreements(RateId),HourlyAmount decimal(18,2) NOT NULL,Amount decimal(18,2) NOT NULL,
 ParentVersion binary(8) NOT NULL,RateVersion binary(8) NOT NULL,SetVersion binary(8) NULL,ApprovalId uniqueidentifier NULL,ApprovalVersion binary(8) NULL,
 CONSTRAINT UX_AccrualSource UNIQUE(SourceKind,SourceId),
 CONSTRAINT CK_AccrualLine CHECK(SourceKind IN(N'Whole',N'Segment') AND PaidMinutes BETWEEN 0 AND 1440 AND HourlyAmount>0 AND Amount=ROUND(CONVERT(decimal(28,8),PaidMinutes)*HourlyAmount/60,2))
);
CREATE INDEX IX_AccrualTimesheet ON dbo.AccrualCalculationLines(TimesheetId);
GO
CREATE TRIGGER dbo.GuardHourlyRates ON dbo.HourlyRateAgreements AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.RateId=D.RateId WHERE I.RateId IS NULL) THROW 51220,N'Tarife geçmişi silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I LEFT JOIN deleted D ON D.RateId=I.RateId WHERE D.RateId IS NULL AND I.RevokedUtc IS NOT NULL) THROW 51221,N'Tarife etkin başlamalı.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.RateId=I.RateId WHERE D.RevokedUtc IS NOT NULL OR I.RevokedUtc IS NULL OR EXISTS(
 SELECT I.ProjectId,I.PersonId,I.EmploymentId,I.ValidFrom,I.ValidTo,I.HourlyAmount,I.Currency COLLATE Latin1_General_100_BIN2,I.CreatedByAccountId,I.CreatedBy COLLATE Latin1_General_100_BIN2,I.CreatedUtc
 EXCEPT SELECT D.ProjectId,D.PersonId,D.EmploymentId,D.ValidFrom,D.ValidTo,D.HourlyAmount,D.Currency COLLATE Latin1_General_100_BIN2,D.CreatedByAccountId,D.CreatedBy COLLATE Latin1_General_100_BIN2,D.CreatedUtc)) THROW 51222,N'Tarife gövdesi değişmez; gerekçeyle kaldırıp yeni tarihli tarife oluşturun.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN dbo.EmploymentPeriods E ON E.EmploymentId=I.EmploymentId WHERE E.PersonId<>I.PersonId) THROW 51223,N'Tarife çalışan dönemine ait olmalı.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN dbo.HourlyRateAgreements R WITH(UPDLOCK,HOLDLOCK) ON R.ProjectId=I.ProjectId AND R.EmploymentId=I.EmploymentId AND R.RateId<>I.RateId AND R.RevokedUtc IS NULL AND R.ValidFrom<=COALESCE(I.ValidTo,CONVERT(date,'99991231')) AND I.ValidFrom<=COALESCE(R.ValidTo,CONVERT(date,'99991231')) WHERE I.RevokedUtc IS NULL) THROW 51224,N'Aynı proje/çalışma döneminde etkin tarife tarihleri çakışamaz.',1;
END;
GO
CREATE TRIGGER dbo.GuardAccrualCalculations ON dbo.AccrualCalculations AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.CalculationId=D.CalculationId WHERE I.CalculationId IS NULL) THROW 51225,N'Dönem hesabı silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I LEFT JOIN deleted D ON D.CalculationId=I.CalculationId WHERE D.CalculationId IS NULL AND I.IsSealed<>0) THROW 51226,N'Dönem hesabı önce hazırlanmalı.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.CalculationId=I.CalculationId WHERE D.IsSealed<>0 OR I.IsSealed<>1 OR EXISTS(
 SELECT I.ProjectId,I.PeriodStart,I.PeriodEnd,I.Policy COLLATE Latin1_General_100_BIN2,I.Currency COLLATE Latin1_General_100_BIN2,I.TotalAmount,I.LineCount,I.SnapshotHash,I.CreatedByAccountId,I.CreatedBy COLLATE Latin1_General_100_BIN2,I.CreatedUtc
 EXCEPT SELECT D.ProjectId,D.PeriodStart,D.PeriodEnd,D.Policy COLLATE Latin1_General_100_BIN2,D.Currency COLLATE Latin1_General_100_BIN2,D.TotalAmount,D.LineCount,D.SnapshotHash,D.CreatedByAccountId,D.CreatedBy COLLATE Latin1_General_100_BIN2,D.CreatedUtc)) THROW 51227,N'Dönem hesabı değişmez; yalnız hazırlığı tamamlanabilir.',1;
 IF EXISTS(SELECT 1 FROM inserted I WHERE I.IsSealed=1 AND ((SELECT COUNT(*) FROM dbo.AccrualCalculationLines L WHERE L.CalculationId=I.CalculationId)<>I.LineCount OR (SELECT SUM(L.Amount) FROM dbo.AccrualCalculationLines L WHERE L.CalculationId=I.CalculationId)<>I.TotalAmount)) THROW 51228,N'Dönem hesabı toplamı/satır sayısı uzlaşmıyor.',1;
END;
GO
CREATE TRIGGER dbo.GuardAccrualLines ON dbo.AccrualCalculationLines AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51229,N'Hakediş hesap satırları değiştirilemez veya silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted L JOIN dbo.AccrualCalculations P ON P.CalculationId=L.CalculationId JOIN dbo.HourlyRateAgreements R ON R.RateId=L.RateId JOIN dbo.Timesheets T ON T.TimesheetId=L.TimesheetId WHERE
 P.IsSealed<>0 OR T.Status<>N'Approved' OR T.PersonId<>L.PersonId OR T.EmploymentId<>L.EmploymentId OR T.WorkDate<>L.WorkDate OR T.RecordVersion<>L.ParentVersion
 OR L.WorkDate<P.PeriodStart OR L.WorkDate>P.PeriodEnd OR R.ProjectId<>P.ProjectId OR R.PersonId<>L.PersonId OR R.EmploymentId<>L.EmploymentId OR R.RevokedUtc IS NOT NULL OR R.RecordVersion<>L.RateVersion OR R.HourlyAmount<>L.HourlyAmount OR R.ValidFrom>L.WorkDate OR (R.ValidTo IS NOT NULL AND R.ValidTo<L.WorkDate)
 OR (L.SourceKind=N'Whole' AND (L.SourceId<>T.TimesheetId OR P.ProjectId<>T.ProjectId OR (T.PayableMinutes IS NULL OR L.PaidMinutes<>T.PayableMinutes) OR L.SetVersion IS NOT NULL OR L.ApprovalId IS NOT NULL OR L.ApprovalVersion IS NOT NULL OR EXISTS(SELECT 1 FROM dbo.TimesheetAllocationSets H WHERE H.TimesheetId=T.TimesheetId AND H.ReleasedUtc IS NULL)))
 OR (L.SourceKind=N'Segment' AND NOT EXISTS(SELECT 1 FROM dbo.TimesheetSegments S JOIN dbo.TimesheetAllocationSets H ON H.SetId=S.SetId WHERE S.SegmentId=L.SourceId AND S.ProjectId=P.ProjectId AND H.TimesheetId=T.TimesheetId AND H.IsSealed=1 AND H.ReleasedUtc IS NULL AND H.RecordVersion=L.SetVersion AND S.PayableMinutes=L.PaidMinutes AND ((H.UsesSegmentApproval=0 AND L.ApprovalId IS NULL AND L.ApprovalVersion IS NULL) OR EXISTS(SELECT 1 FROM dbo.TimesheetApprovalRounds Q JOIN dbo.TimesheetSegmentApprovals D ON D.RoundId=Q.RoundId WHERE Q.SetId=H.SetId AND Q.IsReady=1 AND Q.ClosedUtc IS NULL AND D.SegmentId=S.SegmentId AND D.State=N'Approved' AND D.ApprovalId=L.ApprovalId AND D.RecordVersion=L.ApprovalVersion))))) THROW 51230,N'Hesap satırı güncel onaylı kaynağa/tarifeye ait değil.',1;
END;
GO
CREATE TRIGGER dbo.GuardAccruedTimesheet ON dbo.Timesheets AFTER UPDATE,DELETE AS
BEGIN IF EXISTS(SELECT 1 FROM deleted T JOIN dbo.AccrualCalculationLines L ON L.TimesheetId=T.TimesheetId) THROW 51231,N'Dönem hesabına alınan puantaj değiştirilemez; bağlantılı mali düzeltme gerekir.',1; END;
GO
CREATE TRIGGER dbo.GuardAccruedAllocation ON dbo.TimesheetAllocationSets AFTER UPDATE,DELETE AS
BEGIN IF EXISTS(SELECT 1 FROM deleted H JOIN dbo.AccrualCalculationLines L ON L.TimesheetId=H.TimesheetId) THROW 51232,N'Dönem hesabına alınan dağılım değiştirilemez.',1; END;
GO
CREATE TRIGGER dbo.GuardAccruedDecision ON dbo.TimesheetSegmentApprovals AFTER INSERT,UPDATE,DELETE AS
BEGIN IF EXISTS(SELECT 1 FROM(SELECT SegmentId FROM inserted UNION SELECT SegmentId FROM deleted) X JOIN dbo.TimesheetSegments S ON S.SegmentId=X.SegmentId JOIN dbo.TimesheetAllocationSets H ON H.SetId=S.SetId JOIN dbo.AccrualCalculationLines L ON L.TimesheetId=H.TimesheetId) THROW 51233,N'Dönem hesabına alınan kaydın parça kararı değiştirilemez.',1; END;
GO

CREATE TRIGGER dbo.Audit_HourlyRateAgreements ON dbo.HourlyRateAgreements AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'AccrualFoundation.Change',N'HourlyRateAgreements',CONVERT(nvarchar(100),COALESCE(I.RateId,D.RateId)),COALESCE(I.PersonId,D.PersonId),CASE WHEN D.RateId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',CASE WHEN D.RateId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,CASE WHEN I.RateId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application' FROM inserted I FULL OUTER JOIN deleted D ON D.RateId=I.RateId;
END;
GO
CREATE TRIGGER dbo.Audit_AccrualCalculations ON dbo.AccrualCalculations AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'AccrualFoundation.Change',N'AccrualCalculations',CONVERT(nvarchar(100),COALESCE(I.CalculationId,D.CalculationId)),NULL,CASE WHEN D.CalculationId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',CASE WHEN D.CalculationId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,CASE WHEN I.CalculationId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application' FROM inserted I FULL OUTER JOIN deleted D ON D.CalculationId=I.CalculationId;
END;
GO
CREATE TRIGGER dbo.Audit_AccrualCalculationLines ON dbo.AccrualCalculationLines AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'AccrualFoundation.Change',N'AccrualCalculationLines',CONVERT(nvarchar(100),COALESCE(I.LineId,D.LineId)),COALESCE(I.PersonId,D.PersonId),CASE WHEN D.LineId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',CASE WHEN D.LineId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,CASE WHEN I.LineId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application' FROM inserted I FULL OUTER JOIN deleted D ON D.LineId=I.LineId;
END;
GO
UPDATE dbo.admin SET SessionEpoch=NEWID();
