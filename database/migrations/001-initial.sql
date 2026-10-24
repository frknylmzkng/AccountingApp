-- Fresh installations only. Executed transactionally by AccountingApp.DatabaseSetup.
CREATE TABLE dbo.Calisan
(
    TC bigint NOT NULL CONSTRAINT PK_Calisan PRIMARY KEY,
    AD nvarchar(50) NOT NULL,
    SOYAD nvarchar(50) NOT NULL,
    IBAN nvarchar(34) NOT NULL,
    PARA decimal(18,2) NOT NULL
);

CREATE TABLE dbo.Joker
(
    TC bigint NOT NULL CONSTRAINT PK_Joker PRIMARY KEY,
    AD nvarchar(50) NOT NULL,
    SOYAD nvarchar(50) NOT NULL,
    IBAN nvarchar(34) NOT NULL,
    PARA decimal(18,2) NOT NULL
);

CREATE TABLE dbo.izinler
(
    IzinId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_izinler PRIMARY KEY,
    TC bigint NOT NULL CONSTRAINT FK_izinler_Calisan REFERENCES dbo.Calisan(TC),
    IZINBASLANGIC date NOT NULL,
    IZINBITIS date NOT NULL,
    IzinSuresi AS (DATEDIFF(day, IZINBASLANGIC, IZINBITIS) + 1),
    CONSTRAINT CK_izinler_Tarihler CHECK (IZINBITIS >= IZINBASLANGIC)
);
CREATE INDEX IX_izinler_TC ON dbo.izinler(TC);

CREATE TABLE dbo.admin
(
    username nvarchar(50) NOT NULL CONSTRAINT PK_admin PRIMARY KEY,
    password nvarchar(50) NOT NULL CONSTRAINT DF_admin_password DEFAULT N'',
    password_hash nvarchar(256) NOT NULL,
    CONSTRAINT CK_admin_NoPlaintext CHECK (password = N''),
    CONSTRAINT CK_admin_HashPresent CHECK (LEN(password_hash) > 0)
);
