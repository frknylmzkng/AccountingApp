-- Lock accounts before personnel, matching authorized writes. Rolled back on failure.
UPDATE dbo.admin SET SessionEpoch=NEWID();
-- Keep historical bodies immutable. A separate immutable link table supplies
-- employment ownership without rewriting ledger, closing or finance snapshots.
CREATE TABLE dbo.EmploymentPeriods(
 EmploymentId uniqueidentifier NOT NULL CONSTRAINT PK_EmploymentPeriods PRIMARY KEY DEFAULT NEWID(),
 PersonId uniqueidentifier NOT NULL REFERENCES dbo.PersonnelIdentities(PersonId),
 PersonnelGroup int NOT NULL CHECK(PersonnelGroup IN(0,1)),
 StartDate date NULL, EndDate date NULL, IsLegacy bit NOT NULL,
 IsClosed bit NOT NULL, CreatedUtc datetime2 NULL DEFAULT SYSUTCDATETIME(),
 CreatedBy nvarchar(50) NULL, RecordVersion rowversion NOT NULL,
 CONSTRAINT UX_EmploymentPerson UNIQUE(EmploymentId,PersonId),
 CONSTRAINT CK_EmploymentDates CHECK(StartDate IS NULL OR EndDate IS NULL OR EndDate>=StartDate));
CREATE UNIQUE INDEX UX_ActiveEmployment ON dbo.EmploymentPeriods(PersonId,PersonnelGroup) WHERE IsClosed=0;
CREATE INDEX IX_EmploymentPerson ON dbo.EmploymentPeriods(PersonId,CreatedUtc,EmploymentId);
ALTER TABLE dbo.Calisan ADD CurrentEmploymentId uniqueidentifier NULL;
ALTER TABLE dbo.Joker ADD CurrentEmploymentId uniqueidentifier NULL;
ALTER TABLE dbo.izinler ADD EmploymentId uniqueidentifier NULL;
GO
INSERT dbo.EmploymentPeriods(PersonId,PersonnelGroup,StartDate,EndDate,IsLegacy,IsClosed,CreatedUtc,CreatedBy)
 SELECT PersonId,0,NULL,NULL,1,CASE WHEN ArchivedUtc IS NULL THEN 0 ELSE 1 END,NULL,NULL FROM dbo.Calisan
 UNION ALL SELECT PersonId,1,NULL,NULL,1,CASE WHEN ArchivedUtc IS NULL THEN 0 ELSE 1 END,NULL,NULL FROM dbo.Joker;
UPDATE C SET CurrentEmploymentId=E.EmploymentId FROM dbo.Calisan C JOIN dbo.EmploymentPeriods E ON E.PersonId=C.PersonId AND E.PersonnelGroup=0;
UPDATE J SET CurrentEmploymentId=E.EmploymentId FROM dbo.Joker J JOIN dbo.EmploymentPeriods E ON E.PersonId=J.PersonId AND E.PersonnelGroup=1;
UPDATE I SET EmploymentId=C.CurrentEmploymentId FROM dbo.izinler I JOIN dbo.Calisan C ON C.PersonId=I.PersonId;
ALTER TABLE dbo.Calisan ADD CONSTRAINT FK_CalisanEmployment FOREIGN KEY(CurrentEmploymentId,PersonId) REFERENCES dbo.EmploymentPeriods(EmploymentId,PersonId);
ALTER TABLE dbo.Joker ADD CONSTRAINT FK_JokerEmployment FOREIGN KEY(CurrentEmploymentId,PersonId) REFERENCES dbo.EmploymentPeriods(EmploymentId,PersonId);
ALTER TABLE dbo.izinler ALTER COLUMN EmploymentId uniqueidentifier NOT NULL;
ALTER TABLE dbo.izinler ADD CONSTRAINT FK_LeaveEmployment FOREIGN KEY(EmploymentId,PersonId) REFERENCES dbo.EmploymentPeriods(EmploymentId,PersonId);
CREATE INDEX IX_LeaveEmployment ON dbo.izinler(EmploymentId,IzinId);
CREATE TABLE dbo.EmploymentRecordLinks(
 RecordKind nvarchar(20) NOT NULL CHECK(RecordKind IN(N'Ledger',N'Finance',N'Assignment',N'LeaveHistory')),
 RecordKey nvarchar(64) NOT NULL, EmploymentId uniqueidentifier NOT NULL, PersonId uniqueidentifier NOT NULL,
 CONSTRAINT PK_EmploymentRecordLinks PRIMARY KEY(RecordKind,RecordKey),
 CONSTRAINT FK_EmploymentRecordPerson FOREIGN KEY(EmploymentId,PersonId) REFERENCES dbo.EmploymentPeriods(EmploymentId,PersonId));
CREATE INDEX IX_EmploymentLinks ON dbo.EmploymentRecordLinks(EmploymentId,RecordKind,RecordKey);
INSERT dbo.EmploymentRecordLinks
 SELECT N'Ledger',CONVERT(nvarchar(64),L.LedgerId),E.EmploymentId,L.PersonId FROM dbo.PaymentLedger L JOIN dbo.EmploymentPeriods E ON E.PersonId=L.PersonId AND E.PersonnelGroup=L.PersonnelGroup
 UNION ALL SELECT N'Finance',CONVERT(nvarchar(64),F.EntryId),E.EmploymentId,F.PersonId FROM dbo.FinanceEntries F JOIN dbo.EmploymentPeriods E ON E.PersonId=F.PersonId AND E.PersonnelGroup=F.PersonnelGroup
 UNION ALL SELECT N'Assignment',CONVERT(nvarchar(64),A.AssignmentId),E.EmploymentId,A.PersonId FROM dbo.PersonnelProjectAssignments A JOIN dbo.EmploymentPeriods E ON E.PersonId=A.PersonId AND E.PersonnelGroup=A.PersonnelGroup
 UNION ALL SELECT N'LeaveHistory',CONVERT(nvarchar(64),H.HistoryId),E.EmploymentId,H.PersonId FROM dbo.LeaveHistory H JOIN dbo.EmploymentPeriods E ON E.PersonId=H.PersonId AND E.PersonnelGroup=0;
GO
-- The identity trigger creates the first period only for a genuinely new card.
-- Existing restore operations must explicitly reopen the same period or choose
-- a new period. No old employment dates are inferred from archive timestamps.
DECLARE @table sysname,@group int,@other sysname,@sql nvarchar(max);
DECLARE cards CURSOR LOCAL FAST_FORWARD FOR SELECT * FROM(VALUES(N'Calisan',0,N'Joker'),(N'Joker',1,N'Calisan'))T(TableName,GroupNumber,OtherTable);
OPEN cards;FETCH NEXT FROM cards INTO @table,@group,@other;
WHILE @@FETCH_STATUS=0
BEGIN SET NOCOUNT ON;
 SET @sql=N'CREATE OR ALTER TRIGGER dbo.'+QUOTENAME(@table+N'Identity')+N' ON dbo.'+QUOTENAME(@table)+N' AFTER INSERT,UPDATE AS
 BEGIN SET NOCOUNT ON;
 IF (UPDATE(PersonId) OR UPDATE(TC)) AND EXISTS(SELECT 1 FROM deleted) THROW 51026,N''Kalıcı personel kimliği ve TC değiştirilemez.'',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN dbo.'+QUOTENAME(@other)+N' O ON O.TC=I.TC) THROW 51026,N''TC başka grupta kayıtlı.'',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN dbo.PersonnelIdentities P ON P.PersonId=I.PersonId WHERE I.TC<>P.TC) THROW 51026,N''Kimlik/TC eşleşmiyor.'',1;
 INSERT dbo.PersonnelIdentities(PersonId,TC) SELECT I.PersonId,I.TC FROM inserted I WHERE NOT EXISTS(SELECT 1 FROM dbo.PersonnelIdentities P WHERE P.PersonId=I.PersonId);
 INSERT dbo.EmploymentPeriods(PersonId,PersonnelGroup,IsLegacy,IsClosed,CreatedBy)
 SELECT I.PersonId,'+CONVERT(nvarchar(1),@group)+N',0,0,CONVERT(nvarchar(50),SESSION_CONTEXT(N''Audit.Actor'')) FROM inserted I LEFT JOIN deleted D ON D.PersonId=I.PersonId
 WHERE D.PersonId IS NULL AND I.CurrentEmploymentId IS NULL;
 UPDATE C SET CurrentEmploymentId=E.EmploymentId FROM dbo.'+QUOTENAME(@table)+N' C JOIN inserted I ON I.PersonId=C.PersonId JOIN dbo.EmploymentPeriods E ON E.PersonId=C.PersonId AND E.PersonnelGroup='+CONVERT(nvarchar(1),@group)+N' AND E.IsClosed=0 WHERE C.CurrentEmploymentId IS NULL;
 IF EXISTS(SELECT 1 FROM dbo.'+QUOTENAME(@table)+N' C JOIN inserted I ON I.PersonId=C.PersonId LEFT JOIN dbo.EmploymentPeriods E ON E.EmploymentId=C.CurrentEmploymentId AND E.PersonId=C.PersonId AND E.PersonnelGroup='+CONVERT(nvarchar(1),@group)+N' WHERE E.EmploymentId IS NULL OR (C.ArchivedUtc IS NULL AND E.IsClosed=1)) THROW 51080,N''Personelin çalışma dönemi geçersiz.'',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.PersonId=I.PersonId WHERE D.ArchivedUtc IS NULL AND D.CurrentEmploymentId IS NOT NULL AND ISNULL(I.CurrentEmploymentId,''00000000-0000-0000-0000-000000000000'')<>ISNULL(D.CurrentEmploymentId,''00000000-0000-0000-0000-000000000000'')) THROW 51080,N''Etkin kartın dönemi değiştirilemez.'',1;
 END;';
 EXEC sys.sp_executesql @sql;FETCH NEXT FROM cards INTO @table,@group,@other;
END;
CLOSE cards;DEALLOCATE cards;
GO
CREATE TRIGGER dbo.ProtectEmployment ON dbo.EmploymentPeriods AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.EmploymentId=D.EmploymentId WHERE I.EmploymentId IS NULL) THROW 51081,N'Çalışma dönemi silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.EmploymentId=I.EmploymentId WHERE NOT EXISTS(SELECT 1 FROM dbo.Calisan C WHERE C.CurrentEmploymentId=I.EmploymentId UNION ALL SELECT 1 FROM dbo.Joker J WHERE J.CurrentEmploymentId=I.EmploymentId)) THROW 51081,N'Önceki çalışma dönemleri dondurulmuştur.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.EmploymentId=I.EmploymentId WHERE D.PersonId<>I.PersonId OR D.PersonnelGroup<>I.PersonnelGroup OR D.IsLegacy<>I.IsLegacy OR ISNULL(D.CreatedUtc,'00010101')<>ISNULL(I.CreatedUtc,'00010101') OR ISNULL(D.CreatedBy,N'') COLLATE Latin1_General_100_BIN2<>ISNULL(I.CreatedBy,N'') COLLATE Latin1_General_100_BIN2) THROW 51081,N'Dönem kimliği ve kaynağı değiştirilemez.',1;
END;
GO
CREATE TRIGGER dbo.ProtectEmploymentLinks ON dbo.EmploymentRecordLinks INSTEAD OF UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted) THROW 51082,N'Geçmiş kaydın çalışma dönemi değiştirilemez.',1; END;
GO
CREATE TRIGGER dbo.GuardLeaveEmployment ON dbo.izinler AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.IzinId=D.IzinId JOIN dbo.Calisan C ON C.PersonId=D.PersonId WHERE I.IzinId IS NULL AND (D.EmploymentId<>C.CurrentEmploymentId OR C.ArchivedUtc IS NOT NULL)) THROW 51083,N'Önceki veya arşivli dönemin izin kaydı silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.IzinId=I.IzinId WHERE I.EmploymentId<>D.EmploymentId) THROW 51083,N'İznin dönemi değiştirilemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN dbo.Calisan C ON C.PersonId=I.PersonId JOIN dbo.EmploymentPeriods E ON E.EmploymentId=I.EmploymentId WHERE C.CurrentEmploymentId<>I.EmploymentId OR C.ArchivedUtc IS NOT NULL OR E.IsClosed=1 OR (E.StartDate IS NOT NULL AND I.IZINBASLANGIC<E.StartDate) OR (E.EndDate IS NOT NULL AND I.IZINBITIS>E.EndDate)) THROW 51083,N'İzin güncel çalışma dönemi dışında.',1;
END;
GO
CREATE TRIGGER dbo.LinkEmploymentLedger ON dbo.PaymentLedger AFTER INSERT AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.EmploymentRecordLinks(RecordKind,RecordKey,EmploymentId,PersonId)
 SELECT N'Ledger',CONVERT(nvarchar(64),I.LedgerId),C.CurrentEmploymentId,I.PersonId FROM inserted I
 JOIN(SELECT PersonId,CurrentEmploymentId FROM dbo.Calisan UNION ALL SELECT PersonId,CurrentEmploymentId FROM dbo.Joker)C ON C.PersonId=I.PersonId;
END;
GO
CREATE TRIGGER dbo.LinkEmploymentFinance ON dbo.FinanceEntries AFTER INSERT AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.EmploymentRecordLinks SELECT N'Finance',CONVERT(nvarchar(64),I.EntryId),C.CurrentEmploymentId,I.PersonId FROM inserted I JOIN(SELECT PersonId,CurrentEmploymentId FROM dbo.Calisan UNION ALL SELECT PersonId,CurrentEmploymentId FROM dbo.Joker)C ON C.PersonId=I.PersonId WHERE I.Kind=N'Accrual';
END;
GO
CREATE TRIGGER dbo.LinkEmploymentAssignment ON dbo.PersonnelProjectAssignments AFTER INSERT AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.EmploymentRecordLinks SELECT N'Assignment',CONVERT(nvarchar(64),I.AssignmentId),C.CurrentEmploymentId,I.PersonId FROM inserted I JOIN(SELECT PersonId,CurrentEmploymentId FROM dbo.Calisan UNION ALL SELECT PersonId,CurrentEmploymentId FROM dbo.Joker)C ON C.PersonId=I.PersonId;
END;
GO
CREATE TRIGGER dbo.LinkEmploymentLeaveHistory ON dbo.LeaveHistory AFTER INSERT AS
BEGIN SET NOCOUNT ON;
 -- A current leave deletion is captured by 107 before this nested trigger.
 -- Deleted rows are no longer in izinler; the delete guard guarantees current ownership.
 INSERT dbo.EmploymentRecordLinks SELECT N'LeaveHistory',CONVERT(nvarchar(64),I.HistoryId),COALESCE(L.EmploymentId,C.CurrentEmploymentId),I.PersonId FROM inserted I JOIN dbo.Calisan C ON C.PersonId=I.PersonId LEFT JOIN dbo.izinler L ON L.IzinId=I.IzinId;
END;
GO
CREATE TRIGGER dbo.AuditChanges_EmploymentPeriods ON dbo.EmploymentPeriods AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'Record.Change',N'Employment',CONVERT(nvarchar(100),COALESCE(I.EmploymentId,D.EmploymentId)),COALESCE(I.PersonId,D.PersonId),CASE WHEN D.EmploymentId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),CASE WHEN SESSION_CONTEXT(N'Audit.Reason') IS NULL THEN N'NotProvided' ELSE N'UserProvided' END,
 CASE WHEN D.EmploymentId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 CASE WHEN I.EmploymentId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),CASE WHEN SESSION_CONTEXT(N'Audit.Actor') IS NULL THEN N'DirectSql' ELSE N'Application' END
 FROM inserted I FULL OUTER JOIN deleted D ON D.EmploymentId=I.EmploymentId;
END;
GO
