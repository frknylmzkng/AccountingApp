-- Only cancellation receipts live on the server. Draft contents remain encrypted on the originating Windows profile.
CREATE TABLE dbo.TimesheetCanceledCommands(
 CommandId uniqueidentifier NOT NULL PRIMARY KEY,
 ProjectId uniqueidentifier NOT NULL REFERENCES dbo.FinanceProjects(ProjectId),
 ActorId uniqueidentifier NOT NULL,PayloadHash binary(32) NOT NULL,CreatedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME()
);
GO
CREATE TRIGGER dbo.GuardTimesheetCanceledCommands ON dbo.TimesheetCanceledCommands AFTER UPDATE,DELETE AS
BEGIN THROW 51210,N'Vazgeçilen puantaj isteği kaydı değiştirilemez veya silinemez.',1; END;
GO
CREATE TRIGGER dbo.AuditTimesheetCanceledCommands ON dbo.TimesheetCanceledCommands AFTER INSERT AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'TimesheetDraft.Abandon',N'TimesheetCanceledCommands',CONVERT(nvarchar(100),I.CommandId),N'Insert',
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER),
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application' FROM inserted I;
END;
GO
UPDATE dbo.admin SET SessionEpoch=NEWID();
