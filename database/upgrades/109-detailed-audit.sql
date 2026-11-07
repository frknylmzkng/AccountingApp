-- Existing audit rows retain their original content. Missing historic details stay NULL.
ALTER TABLE dbo.admin ADD AccountId uniqueidentifier NOT NULL CONSTRAINT DF_AdminAccountId DEFAULT NEWID();
GO
CREATE UNIQUE INDEX UX_AdminAccountId ON dbo.admin(AccountId);
-- Old clients must reauthenticate with the version that supplies audit context.
UPDATE dbo.admin SET SessionEpoch=NEWID();
ALTER TABLE dbo.AuditLog ADD EntityType nvarchar(40) NULL, RecordKey nvarchar(100) NULL,
 PersonId uniqueidentifier NULL, Operation nvarchar(12) NULL, Reason nvarchar(500) NULL,
 ReasonSource nvarchar(20) NULL, OldValues nvarchar(max) NULL, NewValues nvarchar(max) NULL,
 CorrelationId uniqueidentifier NULL, ActorAccountId uniqueidentifier NULL, SqlPrincipal nvarchar(128) NULL, Source nvarchar(20) NULL;
GO
ALTER TABLE dbo.AuditLog ADD CONSTRAINT CK_AuditOldJson CHECK(OldValues IS NULL OR ISJSON(OldValues)=1),
 CONSTRAINT CK_AuditNewJson CHECK(NewValues IS NULL OR ISJSON(NewValues)=1);
CREATE INDEX IX_AuditEntity ON dbo.AuditLog(EntityType,RecordKey,AuditId DESC);
CREATE INDEX IX_AuditPerson ON dbo.AuditLog(PersonId,AuditId DESC) WHERE PersonId IS NOT NULL;
GO
CREATE TRIGGER dbo.ProtectAuditLog ON dbo.AuditLog INSTEAD OF UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51045,N'İşlem geçmişi değiştirilemez veya silinemez.',1;
END;
GO
CREATE TRIGGER dbo.AuditChanges_Calisan ON dbo.[Calisan] AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),
 N'Record.Change',N'Personnel.General',CONVERT(nvarchar(100),COALESCE(I.[PersonId],D.[PersonId])),COALESCE(I.PersonId,D.PersonId),
 CASE WHEN D.[PersonId] IS NULL THEN N'Insert' WHEN I.[PersonId] IS NULL THEN N'Delete' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),
 CASE WHEN SESSION_CONTEXT(N'Audit.Reason') IS NULL THEN N'NotProvided' ELSE N'UserProvided' END,
 J.BeforeJson,J.AfterJson,TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),
 CASE WHEN SESSION_CONTEXT(N'Audit.Actor') IS NULL THEN N'DirectSql' ELSE N'Application' END
 FROM inserted I FULL OUTER JOIN deleted D ON I.[PersonId]=D.[PersonId]
 CROSS APPLY(SELECT CASE WHEN D.[PersonId] IS NULL THEN NULL ELSE (SELECT D.[PersonId],D.[TC],D.[AD],D.[SOYAD],D.[IBAN],D.[PARA],D.[ArchivedUtc],D.[ArchivedBy] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END BeforeJson,
 CASE WHEN I.[PersonId] IS NULL THEN NULL ELSE (SELECT I.[PersonId],I.[TC],I.[AD],I.[SOYAD],I.[IBAN],I.[PARA],I.[ArchivedUtc],I.[ArchivedBy] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END AfterJson) J
 WHERE J.BeforeJson IS NULL OR J.AfterJson IS NULL OR J.BeforeJson COLLATE Latin1_General_100_BIN2<>J.AfterJson COLLATE Latin1_General_100_BIN2;
END;
GO
CREATE TRIGGER dbo.AuditChanges_Joker ON dbo.[Joker] AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),
 N'Record.Change',N'Personnel.Joker',CONVERT(nvarchar(100),COALESCE(I.[PersonId],D.[PersonId])),COALESCE(I.PersonId,D.PersonId),
 CASE WHEN D.[PersonId] IS NULL THEN N'Insert' WHEN I.[PersonId] IS NULL THEN N'Delete' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),
 CASE WHEN SESSION_CONTEXT(N'Audit.Reason') IS NULL THEN N'NotProvided' ELSE N'UserProvided' END,
 J.BeforeJson,J.AfterJson,TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),
 CASE WHEN SESSION_CONTEXT(N'Audit.Actor') IS NULL THEN N'DirectSql' ELSE N'Application' END
 FROM inserted I FULL OUTER JOIN deleted D ON I.[PersonId]=D.[PersonId]
 CROSS APPLY(SELECT CASE WHEN D.[PersonId] IS NULL THEN NULL ELSE (SELECT D.[PersonId],D.[TC],D.[AD],D.[SOYAD],D.[IBAN],D.[PARA],D.[ArchivedUtc],D.[ArchivedBy] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END BeforeJson,
 CASE WHEN I.[PersonId] IS NULL THEN NULL ELSE (SELECT I.[PersonId],I.[TC],I.[AD],I.[SOYAD],I.[IBAN],I.[PARA],I.[ArchivedUtc],I.[ArchivedBy] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END AfterJson) J
 WHERE J.BeforeJson IS NULL OR J.AfterJson IS NULL OR J.BeforeJson COLLATE Latin1_General_100_BIN2<>J.AfterJson COLLATE Latin1_General_100_BIN2;
END;
GO
CREATE TRIGGER dbo.AuditChanges_izinler ON dbo.[izinler] AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),
 N'Record.Change',N'Leave',CONVERT(nvarchar(100),COALESCE(I.[IzinId],D.[IzinId])),COALESCE(I.PersonId,D.PersonId),
 CASE WHEN D.[IzinId] IS NULL THEN N'Insert' WHEN I.[IzinId] IS NULL THEN N'Delete' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),
 CASE WHEN SESSION_CONTEXT(N'Audit.Reason') IS NULL THEN N'NotProvided' ELSE N'UserProvided' END,
 J.BeforeJson,J.AfterJson,TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),
 CASE WHEN SESSION_CONTEXT(N'Audit.Actor') IS NULL THEN N'DirectSql' ELSE N'Application' END
 FROM inserted I FULL OUTER JOIN deleted D ON I.[IzinId]=D.[IzinId]
 CROSS APPLY(SELECT CASE WHEN D.[IzinId] IS NULL THEN NULL ELSE (SELECT D.[IzinId],D.[PersonId],D.[TC],D.[IZINBASLANGIC],D.[IZINBITIS],D.[LastChangedBy] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END BeforeJson,
 CASE WHEN I.[IzinId] IS NULL THEN NULL ELSE (SELECT I.[IzinId],I.[PersonId],I.[TC],I.[IZINBASLANGIC],I.[IZINBITIS],I.[LastChangedBy] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END AfterJson) J
 WHERE J.BeforeJson IS NULL OR J.AfterJson IS NULL OR J.BeforeJson COLLATE Latin1_General_100_BIN2<>J.AfterJson COLLATE Latin1_General_100_BIN2;
END;
GO
CREATE TRIGGER dbo.AuditChanges_PaymentLedger ON dbo.[PaymentLedger] AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),
 N'Record.Change',N'Payment',CONVERT(nvarchar(100),COALESCE(I.[LedgerId],D.[LedgerId])),COALESCE(I.PersonId,D.PersonId),
 CASE WHEN D.[LedgerId] IS NULL THEN N'Insert' WHEN I.[LedgerId] IS NULL THEN N'Delete' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),
 CASE WHEN SESSION_CONTEXT(N'Audit.Reason') IS NULL THEN N'NotProvided' ELSE N'UserProvided' END,
 J.BeforeJson,J.AfterJson,TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),
 CASE WHEN SESSION_CONTEXT(N'Audit.Actor') IS NULL THEN N'DirectSql' ELSE N'Application' END
 FROM inserted I FULL OUTER JOIN deleted D ON I.[LedgerId]=D.[LedgerId]
 CROSS APPLY(SELECT CASE WHEN D.[LedgerId] IS NULL THEN NULL ELSE (SELECT D.[LedgerId],D.[PersonId],D.[TC],D.[PersonnelGroup],D.[Delta],D.[Kind],D.[Actor],D.[RequestId],D.[ReversedUtc] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END BeforeJson,
 CASE WHEN I.[LedgerId] IS NULL THEN NULL ELSE (SELECT I.[LedgerId],I.[PersonId],I.[TC],I.[PersonnelGroup],I.[Delta],I.[Kind],I.[Actor],I.[RequestId],I.[ReversedUtc] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END AfterJson) J
 WHERE J.BeforeJson IS NULL OR J.AfterJson IS NULL OR J.BeforeJson COLLATE Latin1_General_100_BIN2<>J.AfterJson COLLATE Latin1_General_100_BIN2;
END;
GO
CREATE TRIGGER dbo.AuditChanges_admin ON dbo.[admin] AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF UPDATE(AccountId) AND EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.AccountId=D.AccountId WHERE I.AccountId IS NULL) AND EXISTS(SELECT 1 FROM inserted) THROW 51046,N'Hesap kimliği değiştirilemez.',1;
 IF UPDATE(AccountId) AND EXISTS(SELECT 1 FROM deleted D INNER JOIN inserted I ON I.username=D.username WHERE I.AccountId<>D.AccountId) THROW 51046,N'Hesap kimliği değiştirilemez.',1;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),
 N'Record.Change',N'Account',CONVERT(nvarchar(100),COALESCE(I.[AccountId],D.[AccountId])),NULL,
 CASE WHEN D.[AccountId] IS NULL THEN N'Insert' WHEN I.[AccountId] IS NULL THEN N'Delete' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),
 CASE WHEN SESSION_CONTEXT(N'Audit.Reason') IS NULL THEN N'NotProvided' ELSE N'UserProvided' END,
 J.BeforeJson,J.AfterJson,TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),
 CASE WHEN SESSION_CONTEXT(N'Audit.Actor') IS NULL THEN N'DirectSql' ELSE N'Application' END
 FROM inserted I FULL OUTER JOIN deleted D ON I.[AccountId]=D.[AccountId]
 CROSS APPLY(SELECT CASE WHEN D.[AccountId] IS NULL THEN NULL ELSE (SELECT D.[AccountId],D.[username],D.[RoleName],D.[IsActive],D.[MustChangePassword],D.[PasswordResetExpiresUtc],D.[CanPay],D.[CanExportPersonalData], CAST(CASE WHEN I.AccountId IS NOT NULL AND D.AccountId IS NOT NULL AND (ISNULL(I.password_hash,N'')<>ISNULL(D.password_hash,N'') OR ISNULL(I.password,N'')<>ISNULL(D.password,N'')) THEN 0 ELSE 0 END AS bit) CredentialChanged FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END BeforeJson,
 CASE WHEN I.[AccountId] IS NULL THEN NULL ELSE (SELECT I.[AccountId],I.[username],I.[RoleName],I.[IsActive],I.[MustChangePassword],I.[PasswordResetExpiresUtc],I.[CanPay],I.[CanExportPersonalData], CAST(CASE WHEN I.AccountId IS NOT NULL AND D.AccountId IS NOT NULL AND (ISNULL(I.password_hash,N'')<>ISNULL(D.password_hash,N'') OR ISNULL(I.password,N'')<>ISNULL(D.password,N'')) THEN 1 ELSE 0 END AS bit) CredentialChanged FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END AfterJson) J
 WHERE J.BeforeJson IS NULL OR J.AfterJson IS NULL OR J.BeforeJson COLLATE Latin1_General_100_BIN2<>J.AfterJson COLLATE Latin1_General_100_BIN2;
END;
GO

