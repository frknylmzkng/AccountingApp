-- Additive upgrade: retain every existing card and leave. Never infer the
-- identity of ledger segments belonging to previously deleted personnel.
-- Acquire the account lock before personnel locks, matching authorized writes.
-- Epoch rotation commits only with the complete migration.
UPDATE dbo.admin SET SessionEpoch=NEWID();
IF EXISTS(SELECT TC FROM (SELECT TC FROM dbo.Calisan UNION ALL SELECT TC FROM dbo.Joker) P GROUP BY TC HAVING COUNT(*)>1)
    THROW 51026,N'Aynı TC birden fazla personel kartında var; kimlik yükseltmesi durduruldu.',1;
GO
CREATE TABLE dbo.PersonnelIdentities(
    PersonId uniqueidentifier NOT NULL CONSTRAINT PK_PersonnelIdentities PRIMARY KEY DEFAULT NEWID(),
    TC bigint NOT NULL CONSTRAINT UX_PersonnelIdentities_TC UNIQUE,
    CreatedUtc datetime2 NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT UX_PersonnelIdentityPair UNIQUE(PersonId,TC));
ALTER TABLE dbo.Calisan ADD PersonId uniqueidentifier NOT NULL DEFAULT NEWID(), ArchivedUtc datetime2 NULL, ArchivedBy nvarchar(50) NULL;
ALTER TABLE dbo.Joker ADD PersonId uniqueidentifier NOT NULL DEFAULT NEWID(), ArchivedUtc datetime2 NULL, ArchivedBy nvarchar(50) NULL;
GO
CREATE UNIQUE INDEX UX_Calisan_PersonId ON dbo.Calisan(PersonId);
CREATE UNIQUE INDEX UX_Calisan_PersonIdTC ON dbo.Calisan(PersonId,TC);
CREATE UNIQUE INDEX UX_Joker_PersonId ON dbo.Joker(PersonId);
INSERT dbo.PersonnelIdentities(PersonId,TC,CreatedUtc)
    SELECT PersonId,TC,NULL FROM dbo.Calisan UNION ALL SELECT PersonId,TC,NULL FROM dbo.Joker;
ALTER TABLE dbo.izinler ADD PersonId uniqueidentifier NULL;
ALTER TABLE dbo.PaymentLedger ADD PersonId uniqueidentifier NULL;
GO
UPDATE I SET PersonId=C.PersonId FROM dbo.izinler I INNER JOIN dbo.Calisan C ON C.TC=I.TC;
IF EXISTS(SELECT 1 FROM dbo.izinler WHERE PersonId IS NULL)
    THROW 51026,N'Personel kartı bulunamayan izin var; yükseltme durduruldu.',1;
ALTER TABLE dbo.izinler ALTER COLUMN PersonId uniqueidentifier NOT NULL;
ALTER TABLE dbo.izinler ADD CONSTRAINT FK_Leave_PersonId FOREIGN KEY(PersonId,TC) REFERENCES dbo.Calisan(PersonId,TC);
CREATE INDEX IX_Leave_PersonId ON dbo.izinler(PersonId);
-- Only the current incarnation's segment can safely be attached. Earlier
-- closed segments remain explicitly unlinked historical records.
UPDATE L SET PersonId=P.PersonId
FROM dbo.PaymentLedger L
INNER JOIN (SELECT TC,0 PersonnelGroup,PersonId FROM dbo.Calisan UNION ALL SELECT TC,1,PersonId FROM dbo.Joker) P
    ON P.TC=L.TC AND P.PersonnelGroup=L.PersonnelGroup
WHERE L.LedgerId>COALESCE((SELECT MAX(C.LedgerId) FROM dbo.PaymentLedger C
    WHERE C.TC=L.TC AND C.PersonnelGroup=L.PersonnelGroup AND C.Kind=N'Closure'),0);
ALTER TABLE dbo.PaymentLedger ADD CONSTRAINT FK_Ledger_PersonId FOREIGN KEY(PersonId,TC) REFERENCES dbo.PersonnelIdentities(PersonId,TC);
CREATE INDEX IX_Ledger_PersonId ON dbo.PaymentLedger(PersonId,LedgerId);
CREATE TABLE dbo.PersonnelArchiveEvents(
    EventId bigint IDENTITY PRIMARY KEY, PersonId uniqueidentifier NOT NULL REFERENCES dbo.PersonnelIdentities(PersonId),
    PersonnelGroup int NOT NULL CHECK(PersonnelGroup IN(0,1)),
    Action nvarchar(16) NOT NULL CHECK(Action IN(N'Archive',N'Restore')),
    CreatedUtc datetime2 NOT NULL DEFAULT SYSUTCDATETIME(), Actor nvarchar(50) NOT NULL, Balance decimal(18,2) NOT NULL);
GO
CREATE TRIGGER dbo.CalisanIdentity ON dbo.Calisan AFTER INSERT,UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF (UPDATE(PersonId) OR UPDATE(TC)) AND EXISTS(SELECT 1 FROM deleted)
        THROW 51026,N'Kalıcı personel kimliği ve TC değiştirilemez.',1;
    IF EXISTS(SELECT 1 FROM inserted I INNER JOIN dbo.Joker J ON I.TC=J.TC)
        THROW 51026,N'Bu TC başka bir personel kartında kayıtlı.',1;
    IF EXISTS(SELECT 1 FROM inserted I INNER JOIN dbo.PersonnelIdentities P ON I.PersonId=P.PersonId WHERE I.TC<>P.TC)
        THROW 51026,N'Personel kimliği başka bir TC ile eşleşiyor.',1;
    INSERT dbo.PersonnelIdentities(PersonId,TC)
        SELECT I.PersonId,I.TC FROM inserted I WHERE NOT EXISTS(SELECT 1 FROM dbo.PersonnelIdentities P WHERE P.PersonId=I.PersonId);
END;
GO
CREATE TRIGGER dbo.JokerIdentity ON dbo.Joker AFTER INSERT,UPDATE AS
BEGIN
    SET NOCOUNT ON;
    IF (UPDATE(PersonId) OR UPDATE(TC)) AND EXISTS(SELECT 1 FROM deleted)
        THROW 51026,N'Kalıcı personel kimliği ve TC değiştirilemez.',1;
    IF EXISTS(SELECT 1 FROM inserted I INNER JOIN dbo.Calisan C ON I.TC=C.TC)
        THROW 51026,N'Bu TC başka bir personel kartında kayıtlı.',1;
    IF EXISTS(SELECT 1 FROM inserted I INNER JOIN dbo.PersonnelIdentities P ON I.PersonId=P.PersonId WHERE I.TC<>P.TC)
        THROW 51026,N'Personel kimliği başka bir TC ile eşleşiyor.',1;
    INSERT dbo.PersonnelIdentities(PersonId,TC)
        SELECT I.PersonId,I.TC FROM inserted I WHERE NOT EXISTS(SELECT 1 FROM dbo.PersonnelIdentities P WHERE P.PersonId=I.PersonId);
END;
