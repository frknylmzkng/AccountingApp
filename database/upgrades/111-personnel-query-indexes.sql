-- Cover active personnel lists and SQL summary scans without changing business data.
CREATE INDEX IX_Calisan_ActiveList ON dbo.Calisan(TC)
 INCLUDE(PersonId,AD,SOYAD,IBAN,PARA) WHERE ArchivedUtc IS NULL;
CREATE INDEX IX_Joker_ActiveList ON dbo.Joker(TC)
 INCLUDE(PersonId,AD,SOYAD,IBAN,PARA) WHERE ArchivedUtc IS NULL;
CREATE INDEX IX_Leave_PersonPeriod ON dbo.izinler(PersonId,IZINBASLANGIC,IZINBITIS)
 INCLUDE(LeaveVersion);
CREATE INDEX IX_Leave_EndPeriod ON dbo.izinler(IZINBITIS,PersonId)
 INCLUDE(IZINBASLANGIC,LeaveVersion);
