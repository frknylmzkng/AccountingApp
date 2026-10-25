IF COL_LENGTH(N'dbo.admin', N'IsActive') IS NULL
    ALTER TABLE dbo.admin ADD IsActive bit NOT NULL CONSTRAINT DF_AdminIsActive DEFAULT(1);
IF COL_LENGTH(N'dbo.admin', N'SessionVersion') IS NULL
    ALTER TABLE dbo.admin ADD SessionVersion rowversion NOT NULL;
IF COL_LENGTH(N'dbo.admin', N'SessionEpoch') IS NULL
    ALTER TABLE dbo.admin ADD SessionEpoch uniqueidentifier NOT NULL CONSTRAINT DF_AdminSessionEpoch DEFAULT(NEWID());
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.admin')
    AND ((name=N'SessionVersion' AND (system_type_id<>189 OR is_nullable=1))
        OR (name=N'IsActive' AND (system_type_id<>104 OR is_nullable=1))
        OR (name=N'SessionEpoch' AND (system_type_id<>36 OR is_nullable=1))))
    THROW 51023,N'Hesap oturum şeması uyumsuz; yükseltme durduruldu.',1;
