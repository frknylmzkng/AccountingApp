-- Account locks precede project/grant locks. Existing global roles are explicit;
-- no project membership or responsible account is inferred from prior activity.
UPDATE dbo.admin SET SessionEpoch=NEWID();
ALTER TABLE dbo.admin DROP CONSTRAINT CK_AdminRole;
ALTER TABLE dbo.admin ADD CONSTRAINT CK_AdminRole CHECK(RoleName IN(N'Admin',N'PersonnelManager',N'Viewer',N'ProjectUser'));
ALTER TABLE dbo.admin ADD CONSTRAINT CK_ProjectUserGlobalPermissions CHECK(RoleName<>N'ProjectUser' OR (CanPay=0 AND CanExportPersonalData=0));
CREATE TABLE dbo.ProjectAccountAccess(
 GrantId uniqueidentifier NOT NULL DEFAULT NEWID() CONSTRAINT PK_ProjectAccountAccess PRIMARY KEY,
 ProjectId uniqueidentifier NOT NULL REFERENCES dbo.FinanceProjects(ProjectId),
 -- Deliberately no FK to a deletable account. Immutable AccountId is never
 -- reused, and an orphan cannot match a live authorized session.
 AccountId uniqueidentifier NOT NULL, IsActive bit NOT NULL DEFAULT 1,
 IsResponsible bit NOT NULL, CanManageWorkforce bit NOT NULL,
 CanViewCosts bit NOT NULL, CanExport bit NOT NULL,
 CreatedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), CreatedBy nvarchar(50) NOT NULL,
 RecordVersion rowversion NOT NULL,
 CONSTRAINT CK_ProjectResponsibleActive CHECK(IsResponsible=0 OR IsActive=1));
CREATE UNIQUE INDEX UX_ProjectAccountActive ON dbo.ProjectAccountAccess(ProjectId,AccountId) WHERE IsActive=1;
CREATE UNIQUE INDEX UX_ProjectResponsible ON dbo.ProjectAccountAccess(ProjectId) WHERE IsActive=1 AND IsResponsible=1;
CREATE INDEX IX_ProjectAccountLookup ON dbo.ProjectAccountAccess(AccountId,ProjectId) INCLUDE(IsActive,CanManageWorkforce,CanViewCosts,CanExport);
GO
CREATE TRIGGER dbo.ProtectProjectAccountAccess ON dbo.ProjectAccountAccess AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.GrantId=D.GrantId WHERE I.GrantId IS NULL) THROW 51090,N'Proje yetki geçmişi silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.GrantId=I.GrantId WHERE I.ProjectId<>D.ProjectId OR I.AccountId<>D.AccountId OR I.CreatedUtc<>D.CreatedUtc OR I.CreatedBy COLLATE Latin1_General_100_BIN2<>D.CreatedBy COLLATE Latin1_General_100_BIN2 OR D.IsActive=0)
  THROW 51090,N'Yetki kimliği ve kaldırılmış eski yetki değiştirilemez.',1;
END;
GO
CREATE TRIGGER dbo.AuditChanges_ProjectAccountAccess ON dbo.ProjectAccountAccess AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'Record.Change',N'ProjectAccess',CONVERT(nvarchar(100),COALESCE(I.GrantId,D.GrantId)),CASE WHEN D.GrantId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),CASE WHEN SESSION_CONTEXT(N'Audit.Reason') IS NULL THEN N'NotProvided' ELSE N'UserProvided' END,
 CASE WHEN D.GrantId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 CASE WHEN I.GrantId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),CASE WHEN SESSION_CONTEXT(N'Audit.Actor') IS NULL THEN N'DirectSql' ELSE N'Application' END
 FROM inserted I FULL OUTER JOIN deleted D ON D.GrantId=I.GrantId;
END;
