-- Additive upgrade: SQL Server maintains a new token for every personnel update.
IF COL_LENGTH(N'dbo.Calisan', N'RecordVersion') IS NULL
    ALTER TABLE dbo.Calisan ADD RecordVersion rowversion NOT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Calisan')
    AND name=N'RecordVersion' AND system_type_id<>189)
    THROW 51022,N'Calisan kayıt sürümü uyumsuz; yükseltme durduruldu.',1;
GO
IF COL_LENGTH(N'dbo.Joker', N'RecordVersion') IS NULL
    ALTER TABLE dbo.Joker ADD RecordVersion rowversion NOT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Joker')
    AND name=N'RecordVersion' AND system_type_id<>189)
    THROW 51022,N'Joker kayıt sürümü uyumsuz; yükseltme durduruldu.',1;
