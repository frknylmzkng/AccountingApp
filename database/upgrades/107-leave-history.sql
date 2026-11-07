-- Preserve the current rows as a baseline. Their original creation time and
-- author are unknown; never invent those values during migration.
IF EXISTS(SELECT 1 FROM dbo.izinler WHERE IZINBASLANGIC IS NULL OR IZINBITIS IS NULL OR IZINBITIS<IZINBASLANGIC)
    THROW 51028,N'Eksik veya ters izin tarihleri var; geçmiş yükseltmesi durduruldu.',1;
ALTER TABLE dbo.izinler ADD LastChangedBy nvarchar(50) NULL;
CREATE TABLE dbo.LeaveHistory(
    HistoryId bigint IDENTITY NOT NULL CONSTRAINT PK_LeaveHistory PRIMARY KEY,
    IzinId int NOT NULL, PersonId uniqueidentifier NOT NULL, TC bigint NOT NULL,
    Operation nvarchar(16) NOT NULL CHECK(Operation IN(N'Baseline',N'Create',N'Change',N'Delete')),
    OldStart date NULL, OldEnd date NULL, NewStart date NULL, NewEnd date NULL,
    OldVersion binary(8) NULL, NewVersion binary(8) NULL,
    ChangedUtc datetime2 NULL, Actor nvarchar(50) NULL, SqlPrincipal nvarchar(128) NULL,
    CONSTRAINT FK_LeaveHistory_Person FOREIGN KEY(PersonId,TC) REFERENCES dbo.PersonnelIdentities(PersonId,TC),
    CONSTRAINT CK_LeaveHistory_Dates CHECK((OldStart IS NULL OR OldEnd>=OldStart) AND (NewStart IS NULL OR NewEnd>=NewStart)));
CREATE INDEX IX_LeaveHistory_Person ON dbo.LeaveHistory(PersonId,HistoryId);
GO
INSERT dbo.LeaveHistory(IzinId,PersonId,TC,Operation,NewStart,NewEnd,NewVersion)
    SELECT IzinId,PersonId,TC,N'Baseline',IZINBASLANGIC,IZINBITIS,LeaveVersion FROM dbo.izinler;
GO
CREATE TRIGGER dbo.CaptureLeaveHistory ON dbo.izinler AFTER INSERT,UPDATE,DELETE AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @actorProvided bit=0;
    IF UPDATE(LastChangedBy) SET @actorProvided=1;
    IF (UPDATE(PersonId) OR UPDATE(TC) OR UPDATE(IzinId)) AND EXISTS(SELECT 1 FROM deleted)
        AND EXISTS(SELECT 1 FROM inserted)
        THROW 51028,N'İzin kaydının personel kimliği değiştirilemez.',1;
    INSERT dbo.LeaveHistory(IzinId,PersonId,TC,Operation,OldStart,OldEnd,NewStart,NewEnd,
        OldVersion,NewVersion,ChangedUtc,Actor,SqlPrincipal)
    SELECT COALESCE(I.IzinId,D.IzinId),COALESCE(I.PersonId,D.PersonId),COALESCE(I.TC,D.TC),
        CASE WHEN D.IzinId IS NULL THEN N'Create' WHEN I.IzinId IS NULL THEN N'Delete' ELSE N'Change' END,
        D.IZINBASLANGIC,D.IZINBITIS,I.IZINBASLANGIC,I.IZINBITIS,D.LeaveVersion,I.LeaveVersion,
        SYSUTCDATETIME(),
        CASE WHEN I.IzinId IS NOT NULL AND @actorProvided=1 THEN I.LastChangedBy ELSE NULL END,
        ORIGINAL_LOGIN()
    FROM inserted I FULL OUTER JOIN deleted D ON I.IzinId=D.IzinId
    WHERE I.IzinId IS NULL OR D.IzinId IS NULL OR EXISTS(
        SELECT I.IZINBASLANGIC,I.IZINBITIS EXCEPT SELECT D.IZINBASLANGIC,D.IZINBITIS);
END;
GO
CREATE TRIGGER dbo.ProtectLeaveHistory ON dbo.LeaveHistory INSTEAD OF UPDATE,DELETE AS
BEGIN
    SET NOCOUNT ON;
    IF EXISTS(SELECT 1 FROM deleted)
        THROW 51028,N'İzin geçmişi değiştirilemez veya silinemez.',1;
END;
