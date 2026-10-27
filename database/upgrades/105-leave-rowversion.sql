-- SQL Server changes the token for every leave update, including identical date values.
IF COL_LENGTH(N'dbo.izinler', N'LeaveVersion') IS NULL
    ALTER TABLE dbo.izinler ADD LeaveVersion rowversion NOT NULL;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.izinler')
    AND name=N'LeaveVersion' AND system_type_id<>189)
    THROW 51025,N'İzin kayıt sürümü uyumsuz; yükseltme durduruldu.',1;
