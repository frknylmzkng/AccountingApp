-- Existing projects acquire unknown optional details; no historical assignment is invented.
ALTER TABLE dbo.FinanceProjects ADD Site nvarchar(100) NULL, CustomerLabel nvarchar(100) NULL,
 StartDate date NULL, EndDate date NULL;
GO
ALTER TABLE dbo.FinanceProjects ADD CONSTRAINT CK_ProjectDates CHECK(StartDate IS NULL OR EndDate IS NULL OR EndDate>=StartDate);
CREATE TABLE dbo.ProjectTeams(
 TeamId uniqueidentifier NOT NULL CONSTRAINT PK_ProjectTeams PRIMARY KEY DEFAULT NEWID(),
 ProjectId uniqueidentifier NOT NULL REFERENCES dbo.FinanceProjects(ProjectId),
 Code nvarchar(30) NOT NULL, Name nvarchar(100) NOT NULL, IsActive bit NOT NULL DEFAULT 1,
 CreatedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedBy nvarchar(50) NOT NULL, RecordVersion rowversion NOT NULL,
 CONSTRAINT UX_ProjectTeamCode UNIQUE(ProjectId,Code), CONSTRAINT UX_TeamProject UNIQUE(TeamId,ProjectId));
CREATE TABLE dbo.PersonnelProjectAssignments(
 AssignmentId uniqueidentifier NOT NULL CONSTRAINT PK_PersonnelProjectAssignments PRIMARY KEY DEFAULT NEWID(),
 ProjectId uniqueidentifier NOT NULL REFERENCES dbo.FinanceProjects(ProjectId),
 TeamId uniqueidentifier NULL, PersonId uniqueidentifier NOT NULL REFERENCES dbo.PersonnelIdentities(PersonId),
 PersonnelGroup int NOT NULL CHECK(PersonnelGroup IN(0,1)), StartDate date NOT NULL, EndDate date NULL,
 CreatedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedBy nvarchar(50) NOT NULL,
 CancelledUtc datetime2 NULL, CancelledBy nvarchar(50) NULL, RecordVersion rowversion NOT NULL,
 CONSTRAINT FK_AssignmentTeam FOREIGN KEY(TeamId,ProjectId) REFERENCES dbo.ProjectTeams(TeamId,ProjectId),
 CONSTRAINT CK_AssignmentDates CHECK(EndDate IS NULL OR EndDate>=StartDate),
 CONSTRAINT CK_AssignmentCancel CHECK((CancelledUtc IS NULL AND CancelledBy IS NULL) OR (CancelledUtc IS NOT NULL AND CancelledBy IS NOT NULL)));
CREATE INDEX IX_AssignmentPerson ON dbo.PersonnelProjectAssignments(PersonId,StartDate) INCLUDE(EndDate,ProjectId,TeamId) WHERE CancelledUtc IS NULL;
CREATE INDEX IX_AssignmentProject ON dbo.PersonnelProjectAssignments(ProjectId,StartDate,AssignmentId) INCLUDE(PersonId,TeamId,EndDate,CancelledUtc);
GO
CREATE TRIGGER dbo.ProtectProjectTeams ON dbo.ProjectTeams AFTER UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.TeamId=D.TeamId WHERE I.TeamId IS NULL)
  THROW 51073,N'Ekip silinemez; pasife alın.',1;
 IF EXISTS(SELECT 1 FROM deleted D JOIN inserted I ON I.TeamId=D.TeamId WHERE D.ProjectId<>I.ProjectId
  OR D.Code COLLATE Latin1_General_100_BIN2<>I.Code COLLATE Latin1_General_100_BIN2 OR D.CreatedUtc<>I.CreatedUtc
  OR D.CreatedBy COLLATE Latin1_General_100_BIN2<>I.CreatedBy COLLATE Latin1_General_100_BIN2)
  THROW 51074,N'Ekip kimliği/kodu/projesi değiştirilemez.',1;
END;
GO
CREATE TRIGGER dbo.ProtectProjectDelete ON dbo.FinanceProjects AFTER DELETE AS
BEGIN
 IF EXISTS(SELECT 1 FROM deleted) THROW 51075,N'Proje silinemez; pasife alın.',1;
END;
GO
CREATE TRIGGER dbo.ProtectProjectAssignments ON dbo.PersonnelProjectAssignments AFTER UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.AssignmentId=D.AssignmentId WHERE I.AssignmentId IS NULL)
  THROW 51070,N'Atama silinemez; gerekçeli iptal kullanın.',1;
 IF EXISTS(SELECT 1 FROM deleted D JOIN inserted I ON I.AssignmentId=D.AssignmentId WHERE
  D.ProjectId<>I.ProjectId OR D.PersonId<>I.PersonId OR D.PersonnelGroup<>I.PersonnelGroup
  OR ISNULL(D.TeamId,'00000000-0000-0000-0000-000000000000')<>ISNULL(I.TeamId,'00000000-0000-0000-0000-000000000000')
  OR D.StartDate<>I.StartDate OR ISNULL(D.EndDate,'99991231')<>ISNULL(I.EndDate,'99991231')
  OR D.CreatedUtc<>I.CreatedUtc OR D.CreatedBy COLLATE Latin1_General_100_BIN2<>I.CreatedBy COLLATE Latin1_General_100_BIN2
  OR D.CancelledUtc IS NOT NULL OR I.CancelledUtc IS NULL)
  THROW 51071,N'Atama değiştirilemez veya iptal geri alınamaz; yeni atama oluşturun.',1;
END;
GO
-- Full snapshots include the new fields; project/teams/assignment audit is in the same transaction.
DECLARE @table sysname,@key sysname,@entity nvarchar(50),@person nvarchar(100),@sql nvarchar(max);
DECLARE audit_tables CURSOR LOCAL FAST_FORWARD FOR SELECT * FROM (VALUES
 (N'FinanceProjects',N'ProjectId',N'Finance.Project',N'NULL'),
 (N'ProjectTeams',N'TeamId',N'Project.Team',N'NULL'),
 (N'PersonnelProjectAssignments',N'AssignmentId',N'Project.Assignment',N'COALESCE(I.PersonId,D.PersonId)')) T(TableName,KeyName,Entity,PersonExpr);
OPEN audit_tables; FETCH NEXT FROM audit_tables INTO @table,@key,@entity,@person;
WHILE @@FETCH_STATUS=0
BEGIN
 SET @sql=N'CREATE OR ALTER TRIGGER dbo.'+QUOTENAME(N'AuditChanges_'+@table)+N' ON dbo.'+QUOTENAME(@table)+N' AFTER INSERT,UPDATE,DELETE AS
 BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N''Audit.Actor'')),LEFT(ORIGINAL_LOGIN(),50)),N''Record.Change'',N'''+@entity+N''',CONVERT(nvarchar(100),COALESCE(I.'+QUOTENAME(@key)+N',D.'+QUOTENAME(@key)+N')),'+@person+N',
 CASE WHEN D.'+QUOTENAME(@key)+N' IS NULL THEN N''Insert'' WHEN I.'+QUOTENAME(@key)+N' IS NULL THEN N''Delete'' ELSE N''Update'' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N''Audit.Reason'')),CASE WHEN SESSION_CONTEXT(N''Audit.Reason'') IS NULL THEN N''NotProvided'' ELSE N''UserProvided'' END,
 CASE WHEN D.'+QUOTENAME(@key)+N' IS NULL THEN NULL ELSE (SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END,
 CASE WHEN I.'+QUOTENAME(@key)+N' IS NULL THEN NULL ELSE (SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N''Audit.Correlation'')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N''Audit.ActorId'')),ORIGINAL_LOGIN(),
 CASE WHEN SESSION_CONTEXT(N''Audit.Actor'') IS NULL THEN N''DirectSql'' ELSE N''Application'' END
 FROM inserted I FULL OUTER JOIN deleted D ON I.'+QUOTENAME(@key)+N'=D.'+QUOTENAME(@key)+N'; END;';
 EXEC sys.sp_executesql @sql;
 FETCH NEXT FROM audit_tables INTO @table,@key,@entity,@person;
END;
CLOSE audit_tables; DEALLOCATE audit_tables;
UPDATE dbo.admin SET SessionEpoch=NEWID();
