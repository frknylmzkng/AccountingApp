IF COL_LENGTH(N'dbo.admin',N'MustChangePassword') IS NULL
    ALTER TABLE dbo.admin ADD MustChangePassword bit NOT NULL CONSTRAINT DF_AdminMustChangePassword DEFAULT(0);
IF COL_LENGTH(N'dbo.admin',N'PasswordResetExpiresUtc') IS NULL
    ALTER TABLE dbo.admin ADD PasswordResetExpiresUtc datetime2 NULL;
IF EXISTS(SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.admin')
    AND ((name=N'MustChangePassword' AND (system_type_id<>104 OR is_nullable=1))
        OR (name=N'PasswordResetExpiresUtc' AND (system_type_id<>42 OR is_nullable<>1))))
    THROW 51025,N'Parola yenileme şeması uyumsuz; yükseltme durduruldu.',1;
