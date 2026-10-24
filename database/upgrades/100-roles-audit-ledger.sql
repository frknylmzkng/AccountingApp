IF COL_LENGTH(N'dbo.admin', N'RoleName') IS NULL
    ALTER TABLE dbo.admin ADD RoleName nvarchar(24) NOT NULL CONSTRAINT DF_AdminRole DEFAULT N'Admin';
GO
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name=N'CK_AdminRole')
    ALTER TABLE dbo.admin ADD CONSTRAINT CK_AdminRole CHECK(RoleName IN(N'Admin',N'PersonnelManager',N'Viewer'));
GO
CREATE TABLE dbo.AuditLog(
    AuditId bigint IDENTITY PRIMARY KEY, CreatedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(),
    Actor nvarchar(50) NOT NULL, Action nvarchar(64) NOT NULL);
CREATE TABLE dbo.PaymentLedger(
    LedgerId bigint IDENTITY PRIMARY KEY, TC bigint NOT NULL, PersonnelGroup int NOT NULL CHECK(PersonnelGroup IN(0,1)),
    CreatedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), Delta decimal(18,2) NOT NULL,
    Kind nvarchar(20) NOT NULL CHECK(Kind IN(N'Opening',N'Allocation',N'Payment',N'Reversal',N'Closure')),
    Actor nvarchar(50) NOT NULL, RequestId uniqueidentifier NULL, ReversedUtc datetime2 NULL);
CREATE UNIQUE INDEX UX_PaymentRequest ON dbo.PaymentLedger(RequestId) WHERE RequestId IS NOT NULL;
CREATE INDEX IX_PaymentPerson ON dbo.PaymentLedger(TC,PersonnelGroup,LedgerId);
INSERT INTO dbo.PaymentLedger(TC,PersonnelGroup,Delta,Kind,Actor)
    SELECT TC,0,COALESCE(PARA,0),N'Opening',N'schema-upgrade' FROM dbo.Calisan
    UNION ALL SELECT TC,1,COALESCE(PARA,0),N'Opening',N'schema-upgrade' FROM dbo.Joker;
