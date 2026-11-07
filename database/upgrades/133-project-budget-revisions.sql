CREATE TABLE dbo.ProjectBudgetRevisions(
 BudgetId uniqueidentifier NOT NULL PRIMARY KEY,ProjectId uniqueidentifier NOT NULL REFERENCES dbo.FinanceProjects(ProjectId),
 PreviousBudgetId uniqueidentifier NULL,[Sequence] bigint NOT NULL IDENTITY UNIQUE,
 LaborBudget decimal(18,2) NOT NULL,OtherBudget decimal(18,2) NOT NULL,Currency nvarchar(3) NOT NULL DEFAULT N'TRY',
 Reason nvarchar(500) NOT NULL,CreatedByAccountId uniqueidentifier NOT NULL,CreatedBy nvarchar(50) NOT NULL,
 CreatedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),PayloadHash binary(32) NOT NULL,RecordVersion rowversion NOT NULL,
 CONSTRAINT UX_ProjectBudgetIdentity UNIQUE(ProjectId,BudgetId),
 CONSTRAINT FK_PreviousProjectBudget FOREIGN KEY(ProjectId,PreviousBudgetId) REFERENCES dbo.ProjectBudgetRevisions(ProjectId,BudgetId),
 CONSTRAINT CK_ProjectBudgetAmounts CHECK(LaborBudget>=0 AND OtherBudget>=0 AND LaborBudget+OtherBudget<=9999999999999999.99),
 CONSTRAINT CK_ProjectBudgetCurrency CHECK(Currency=N'TRY'),
 CONSTRAINT CK_ProjectBudgetPrevious CHECK(PreviousBudgetId IS NULL OR PreviousBudgetId<>BudgetId)
);
CREATE UNIQUE INDEX UX_ProjectFirstBudget ON dbo.ProjectBudgetRevisions(ProjectId) WHERE PreviousBudgetId IS NULL;
CREATE UNIQUE INDEX UX_ProjectNextBudget ON dbo.ProjectBudgetRevisions(PreviousBudgetId) WHERE PreviousBudgetId IS NOT NULL;
CREATE INDEX IX_ProjectBudgetLatest ON dbo.ProjectBudgetRevisions(ProjectId,[Sequence] DESC);
GO
CREATE TRIGGER dbo.GuardProjectBudgetRevision ON dbo.ProjectBudgetRevisions AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51290,N'Bütçe geçmişi değişmez; yeni gerekçeli revizyon oluşturun.',1;
 IF EXISTS(SELECT 1 FROM inserted I OUTER APPLY(SELECT TOP(1) B.BudgetId FROM dbo.ProjectBudgetRevisions B WHERE B.ProjectId=I.ProjectId AND B.[Sequence]<I.[Sequence] ORDER BY B.[Sequence] DESC) P
 WHERE EXISTS(SELECT P.BudgetId EXCEPT SELECT I.PreviousBudgetId) OR NOT EXISTS(SELECT 1 FROM dbo.FinanceProjects F WHERE F.ProjectId=I.ProjectId AND F.IsActive=1))
 THROW 51291,N'Bütçe güncel önceki revizyona ve etkin projeye ait olmalı.',1;
END;
GO
CREATE TRIGGER dbo.Audit_ProjectBudgetRevisions ON dbo.ProjectBudgetRevisions AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'ProjectBudget.Change',N'ProjectBudgetRevisions',CONVERT(nvarchar(100),COALESCE(I.BudgetId,D.BudgetId)),CASE WHEN D.BudgetId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',CASE WHEN D.BudgetId IS NULL THEN NULL ELSE(SELECT D.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,CASE WHEN I.BudgetId IS NULL THEN NULL ELSE(SELECT I.* FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application' FROM inserted I FULL OUTER JOIN deleted D ON D.BudgetId=I.BudgetId;
END;
GO
UPDATE dbo.admin SET SessionEpoch=NEWID();
