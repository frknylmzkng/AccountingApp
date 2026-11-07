-- Explicit grants start closed for non-admins; Admin has effective full access.
ALTER TABLE dbo.admin ADD CanViewSalary bit NOT NULL CONSTRAINT DF_AdminCanViewSalary DEFAULT 0,
 CanViewIban bit NOT NULL CONSTRAINT DF_AdminCanViewIban DEFAULT 0;
GO
CREATE OR ALTER FUNCTION dbo.AuditSafeJson(@json nvarchar(max)) RETURNS nvarchar(max) AS
BEGIN
 IF @json IS NULL RETURN NULL;
 IF ISJSON(@json)<>1 OR LEFT(LTRIM(@json),1)<>N'{' RETURN N'{"_privacy":"MASKELENDİ"}';
 DECLARE @body nvarchar(max);
 SELECT @body=STRING_AGG(CONVERT(nvarchar(max),N'"'+STRING_ESCAPE([key],'json')+N'":'+
 CASE WHEN [type]=0 THEN N'null'
 WHEN [key] COLLATE Latin1_General_100_BIN2 IN (N'PersonId',N'EmploymentId',N'CurrentEmploymentId',N'PersonnelGroup',N'IzinId',N'LedgerId',N'EntryId',N'ProjectId',N'TeamId',N'AssignmentId',N'GrantId',N'AccountId',N'FinanceEntryId',N'RequestId',N'ReversesLedgerId',N'RecordVersion',N'RoleName',N'IsActive',N'IsLegacy',N'IsClosed',N'IsCurrent',N'MustChangePassword',N'PasswordResetExpiresUtc',N'CanPay',N'CanExportPersonalData',N'CanViewSalary',N'CanViewIban',N'CredentialChanged',N'IsResponsible',N'CanManageAssignments',N'CanViewCosts',N'CanExport',N'StartDate',N'EndDate',N'PeriodStart',N'PeriodEnd',N'IZINBASLANGIC',N'IZINBITIS',N'CreatedUtc',N'ArchivedUtc',N'ReversedUtc',N'CancelledUtc',N'RevokedUtc',N'BusinessDate',N'TimeZoneId',N'RangeStartUtc',N'RangeEndUtc',N'ClosedUtc',N'PaymentCount',N'ReversalCount',N'CashOut',N'CashReturn',N'NetCash',N'DeclaredCash',N'Difference',N'SnapshotHash',N'Amount',N'PARA',N'Delta',N'Balance',N'Kind',N'PaymentPurpose',N'Rows',N'Format') AND [type] IN(1,2,3)
 THEN CASE WHEN [type]=1 THEN N'"'+STRING_ESCAPE(value,'json')+N'"' ELSE value END
 ELSE N'"[MASKELENDİ]"' END),N',') FROM OPENJSON(@json);
 RETURN N'{'+COALESCE(@body,N'')+N'}';
END;
GO
CREATE OR ALTER TRIGGER dbo.AuditChanges_admin ON dbo.[admin] AFTER INSERT,UPDATE,DELETE AS
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
 CROSS APPLY(SELECT CASE WHEN D.[AccountId] IS NULL THEN NULL ELSE (SELECT D.[AccountId],D.[username],D.[RoleName],D.[IsActive],D.[MustChangePassword],D.[PasswordResetExpiresUtc],D.[CanPay],D.[CanExportPersonalData],D.[CanViewSalary],D.[CanViewIban], CAST(CASE WHEN I.AccountId IS NOT NULL AND D.AccountId IS NOT NULL AND (ISNULL(I.password_hash,N'')<>ISNULL(D.password_hash,N'') OR ISNULL(I.password,N'')<>ISNULL(D.password,N'')) THEN 0 ELSE 0 END AS bit) CredentialChanged FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END BeforeJson,
 CASE WHEN I.[AccountId] IS NULL THEN NULL ELSE (SELECT I.[AccountId],I.[username],I.[RoleName],I.[IsActive],I.[MustChangePassword],I.[PasswordResetExpiresUtc],I.[CanPay],I.[CanExportPersonalData],I.[CanViewSalary],I.[CanViewIban], CAST(CASE WHEN I.AccountId IS NOT NULL AND D.AccountId IS NOT NULL AND (ISNULL(I.password_hash,N'')<>ISNULL(D.password_hash,N'') OR ISNULL(I.password,N'')<>ISNULL(D.password,N'')) THEN 1 ELSE 0 END AS bit) CredentialChanged FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END AfterJson) J
 WHERE J.BeforeJson IS NULL OR J.AfterJson IS NULL OR J.BeforeJson COLLATE Latin1_General_100_BIN2<>J.AfterJson COLLATE Latin1_General_100_BIN2;
END;
GO


UPDATE dbo.admin SET SessionEpoch=NEWID();
