CREATE TABLE dbo.LoginThrottle(
    AccountEpoch uniqueidentifier NOT NULL CONSTRAINT PK_LoginThrottle PRIMARY KEY,
    FailureCount int NOT NULL CONSTRAINT CK_LoginThrottle_Count CHECK(FailureCount BETWEEN 1 AND 5),
    WindowStartedUtc datetime2 NOT NULL,
    LockedUntilUtc datetime2 NULL
);
