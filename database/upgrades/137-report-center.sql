-- Private report presets. Scope is the database, immutable project and account
-- identity; usernames and client supplied account IDs never select an owner.
CREATE TABLE dbo.ReportPresets(
 PresetId uniqueidentifier NOT NULL PRIMARY KEY,
 ProjectId uniqueidentifier NOT NULL REFERENCES dbo.FinanceProjects(ProjectId),
 OwnerAccountId uniqueidentifier NOT NULL,
 Name nvarchar(80) NOT NULL,
 DefinitionJson nvarchar(2000) NOT NULL,
 CreatedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
 CONSTRAINT CK_ReportPresetJson CHECK(ISJSON(DefinitionJson)=1),
 CONSTRAINT CK_ReportPresetName CHECK(LEN(LTRIM(RTRIM(Name))) BETWEEN 1 AND 80)
);
CREATE INDEX IX_ReportPresets_OwnerProject ON dbo.ReportPresets(OwnerAccountId,ProjectId,PresetId);
