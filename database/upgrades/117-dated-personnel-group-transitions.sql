-- Add only a dated transition model; retain both group cards and all old links.
UPDATE dbo.admin SET SessionEpoch=NEWID();
CREATE UNIQUE INDEX UX_OneOpenEmploymentPerPerson ON dbo.EmploymentPeriods(PersonId) WHERE IsClosed=0;
CREATE TABLE dbo.PersonnelGroupTransitions(
 TransitionId uniqueidentifier NOT NULL CONSTRAINT PK_PersonnelGroupTransitions PRIMARY KEY,
 PersonId uniqueidentifier NOT NULL REFERENCES dbo.PersonnelIdentities(PersonId),
 SourceGroup int NOT NULL, TargetGroup int NOT NULL, EffectiveDate date NOT NULL,
 SourceEmploymentId uniqueidentifier NOT NULL, TargetEmploymentId uniqueidentifier NOT NULL,
 SourcePersonVersion binary(8) NOT NULL, SourceEmploymentVersion binary(8) NOT NULL,
 CreatedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), Actor nvarchar(50) NOT NULL, Reason nvarchar(500) NOT NULL,
 CONSTRAINT CK_GroupTransitionGroups CHECK(SourceGroup IN(0,1) AND TargetGroup IN(0,1) AND SourceGroup<>TargetGroup),
 CONSTRAINT FK_GroupTransitionSource FOREIGN KEY(SourceEmploymentId,PersonId) REFERENCES dbo.EmploymentPeriods(EmploymentId,PersonId),
 CONSTRAINT FK_GroupTransitionTarget FOREIGN KEY(TargetEmploymentId,PersonId) REFERENCES dbo.EmploymentPeriods(EmploymentId,PersonId),
 CONSTRAINT UX_GroupTransitionSource UNIQUE(SourceEmploymentId),
 CONSTRAINT UX_GroupTransitionTarget UNIQUE(TargetEmploymentId));
CREATE INDEX IX_GroupTransitionPerson ON dbo.PersonnelGroupTransitions(PersonId,EffectiveDate,TransitionId);
GO
DECLARE @table sysname,@group int,@other sysname,@sql nvarchar(max);
DECLARE cards CURSOR LOCAL FAST_FORWARD FOR SELECT * FROM(VALUES(N'Calisan',0,N'Joker'),(N'Joker',1,N'Calisan'))T(TableName,GroupNumber,OtherTable);
OPEN cards;FETCH NEXT FROM cards INTO @table,@group,@other;
WHILE @@FETCH_STATUS=0
BEGIN SET NOCOUNT ON;
 SET @sql=N'CREATE OR ALTER TRIGGER dbo.'+QUOTENAME(@table+N'Identity')+N' ON dbo.'+QUOTENAME(@table)+N' AFTER INSERT,UPDATE AS
 BEGIN SET NOCOUNT ON;
 IF (UPDATE(PersonId) OR UPDATE(TC)) AND EXISTS(SELECT 1 FROM deleted) THROW 51026,N''Kalıcı personel kimliği ve TC değiştirilemez.'',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN dbo.'+QUOTENAME(@other)+N' O WITH(UPDLOCK,HOLDLOCK) ON O.TC=I.TC WHERE O.PersonId<>I.PersonId OR (O.ArchivedUtc IS NULL AND I.ArchivedUtc IS NULL)) THROW 51026,N''Kimlik başka kişiye ait veya iki grup birden etkin.'',1;
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
CREATE OR ALTER TRIGGER dbo.LinkEmploymentLedger ON dbo.PaymentLedger AFTER INSERT AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.EmploymentRecordLinks(RecordKind,RecordKey,EmploymentId,PersonId)
 SELECT N'Ledger',CONVERT(nvarchar(64),I.LedgerId),C.CurrentEmploymentId,I.PersonId FROM inserted I
 JOIN(SELECT PersonId,CurrentEmploymentId,0 PersonnelGroup FROM dbo.Calisan UNION ALL SELECT PersonId,CurrentEmploymentId,1 PersonnelGroup FROM dbo.Joker)C ON C.PersonId=I.PersonId AND C.PersonnelGroup=I.PersonnelGroup;
END;
GO
CREATE OR ALTER TRIGGER dbo.LinkEmploymentFinance ON dbo.FinanceEntries AFTER INSERT AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.EmploymentRecordLinks SELECT N'Finance',CONVERT(nvarchar(64),I.EntryId),C.CurrentEmploymentId,I.PersonId FROM inserted I JOIN(SELECT PersonId,CurrentEmploymentId,0 PersonnelGroup FROM dbo.Calisan UNION ALL SELECT PersonId,CurrentEmploymentId,1 PersonnelGroup FROM dbo.Joker)C ON C.PersonId=I.PersonId AND C.PersonnelGroup=I.PersonnelGroup WHERE I.Kind=N'Accrual';
END;
GO
CREATE OR ALTER TRIGGER dbo.LinkEmploymentAssignment ON dbo.PersonnelProjectAssignments AFTER INSERT AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.EmploymentRecordLinks SELECT N'Assignment',CONVERT(nvarchar(64),I.AssignmentId),C.CurrentEmploymentId,I.PersonId FROM inserted I JOIN(SELECT PersonId,CurrentEmploymentId,0 PersonnelGroup FROM dbo.Calisan UNION ALL SELECT PersonId,CurrentEmploymentId,1 PersonnelGroup FROM dbo.Joker)C ON C.PersonId=I.PersonId AND C.PersonnelGroup=I.PersonnelGroup;
END;
GO
CREATE TRIGGER dbo.ProtectPersonnelGroupTransitions ON dbo.PersonnelGroupTransitions INSTEAD OF UPDATE,DELETE AS
BEGIN SET NOCOUNT ON; IF EXISTS(SELECT 1 FROM deleted) THROW 51101,N'Grup geçiş geçmişi değiştirilemez veya silinemez.',1; END;
GO
CREATE TRIGGER dbo.ValidatePersonnelGroupTransition ON dbo.PersonnelGroupTransitions AFTER INSERT AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted I JOIN dbo.EmploymentPeriods S ON S.EmploymentId=I.SourceEmploymentId
 JOIN dbo.EmploymentPeriods T ON T.EmploymentId=I.TargetEmploymentId
 WHERE S.PersonnelGroup<>I.SourceGroup OR T.PersonnelGroup<>I.TargetGroup OR S.IsClosed<>1 OR T.IsClosed<>0
 OR S.EndDate IS NULL OR T.StartDate IS NULL OR T.StartDate<>I.EffectiveDate OR DATEDIFF(day,S.EndDate,I.EffectiveDate)<>1)
 THROW 51102,N'Grup geçişinin tarih/dönem bağlantıları geçersiz.',1;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT I.Actor,N'Personnel.GroupTransition',N'PersonnelGroupTransition',CONVERT(nvarchar(100),I.TransitionId),I.PersonId,N'Change',I.Reason,N'UserProvided',
 (SELECT I.SourceEmploymentId EmploymentId,I.SourceGroup PersonnelGroup,DATEADD(day,-1,I.EffectiveDate) EndDate FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),
 (SELECT I.TargetEmploymentId EmploymentId,I.TargetGroup PersonnelGroup,I.EffectiveDate StartDate FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),
 CASE WHEN SESSION_CONTEXT(N'Audit.Actor') IS NULL THEN N'DirectSql' ELSE N'Application' END FROM inserted I;
END;
GO
CREATE TRIGGER dbo.ProtectGroupPeriodBoundary ON dbo.EmploymentPeriods AFTER UPDATE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.EmploymentId=I.EmploymentId
 JOIN dbo.PersonnelGroupTransitions G ON G.SourceEmploymentId=I.EmploymentId)
 THROW 51103,N'Geçişle kapanan grup dönemi dondurulmuştur.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN dbo.PersonnelGroupTransitions G ON G.TargetEmploymentId=I.EmploymentId
 WHERE I.StartDate IS NULL OR I.StartDate<>G.EffectiveDate)
 THROW 51103,N'Grup başlangıcı geçiş tarihinden ayrı değiştirilemez.',1;
END;
GO
-- A retained card in the old group is not the person's latest employment.
-- In particular, UndoArchive must not reopen it after a later cross-group period.
CREATE TRIGGER dbo.ProtectHistoricalGroupPeriod ON dbo.EmploymentPeriods AFTER UPDATE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.EmploymentId=I.EmploymentId
 JOIN dbo.EmploymentPeriods N WITH(HOLDLOCK) ON N.PersonId=D.PersonId AND N.PersonnelGroup<>D.PersonnelGroup
 WHERE (N.StartDate IS NOT NULL AND (D.StartDate IS NULL OR N.StartDate>D.StartDate))
 OR (N.CreatedUtc IS NOT NULL AND D.CreatedUtc IS NOT NULL AND N.CreatedUtc>D.CreatedUtc))
 THROW 51104,N'Kişinin daha yeni bir grup dönemi var; eski dönem düzenlenemez veya yeniden açılamaz.',1;
END;
GO
