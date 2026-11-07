-- Keep all existing immutable audit bodies. Apply minimization to future inserts
-- and a safe projection to every application read, including historical records.
ALTER TABLE dbo.AuditLog ADD ChangedFields nvarchar(max) NULL;
GO
ALTER TABLE dbo.AuditLog ADD CONSTRAINT CK_AuditChangedFieldsJson CHECK(ChangedFields IS NULL OR ISJSON(ChangedFields)=1);
GO
CREATE FUNCTION dbo.AuditSafeJson(@json nvarchar(max)) RETURNS nvarchar(max) AS
BEGIN
 IF @json IS NULL RETURN NULL;
 IF ISJSON(@json)<>1 OR LEFT(LTRIM(@json),1)<>N'{' RETURN N'{"_privacy":"MASKELENDİ"}';
 DECLARE @body nvarchar(max);
 SELECT @body=STRING_AGG(CONVERT(nvarchar(max),N'"'+STRING_ESCAPE([key],'json')+N'":'+
 CASE WHEN [type]=0 THEN N'null'
 WHEN [key] COLLATE Latin1_General_100_BIN2 IN (N'PersonId',N'EmploymentId',N'CurrentEmploymentId',N'PersonnelGroup',N'IzinId',N'LedgerId',N'EntryId',N'ProjectId',N'TeamId',N'AssignmentId',N'GrantId',N'AccountId',N'FinanceEntryId',N'RequestId',N'ReversesLedgerId',N'RecordVersion',N'RoleName',N'IsActive',N'IsLegacy',N'IsClosed',N'IsCurrent',N'MustChangePassword',N'PasswordResetExpiresUtc',N'CanPay',N'CanExportPersonalData',N'CredentialChanged',N'IsResponsible',N'CanManageAssignments',N'CanViewCosts',N'CanExport',N'StartDate',N'EndDate',N'PeriodStart',N'PeriodEnd',N'IZINBASLANGIC',N'IZINBITIS',N'CreatedUtc',N'ArchivedUtc',N'ReversedUtc',N'CancelledUtc',N'RevokedUtc',N'BusinessDate',N'TimeZoneId',N'RangeStartUtc',N'RangeEndUtc',N'ClosedUtc',N'PaymentCount',N'ReversalCount',N'CashOut',N'CashReturn',N'NetCash',N'DeclaredCash',N'Difference',N'SnapshotHash',N'Amount',N'PARA',N'Delta',N'Balance',N'Kind',N'PaymentPurpose',N'Rows',N'Format') AND [type] IN(1,2,3)
 THEN CASE WHEN [type]=1 THEN N'"'+STRING_ESCAPE(value,'json')+N'"' ELSE value END
 ELSE N'"[MASKELENDİ]"' END),N',') FROM OPENJSON(@json);
 RETURN N'{'+COALESCE(@body,N'')+N'}';
END;
GO
CREATE FUNCTION dbo.AuditChangedFields(@before nvarchar(max),@after nvarchar(max)) RETURNS nvarchar(max) AS
BEGIN
 -- The values are compared before minimization. Names/IBAN changes remain
 -- visible even when both masked values display the same marker.
 IF (@before IS NOT NULL AND (ISJSON(@before)<>1 OR LEFT(LTRIM(@before),1)<>N'{'))
 OR (@after IS NOT NULL AND (ISJSON(@after)<>1 OR LEFT(LTRIM(@after),1)<>N'{')) RETURN NULL;
 DECLARE @fields nvarchar(max);
 SELECT @fields=STRING_AGG(CONVERT(nvarchar(max),N'"'+STRING_ESCAPE(K.[key],'json')+N'"'),N',')
 FROM (SELECT [key] COLLATE Latin1_General_100_BIN2 [key] FROM OPENJSON(@before)
       UNION SELECT [key] COLLATE Latin1_General_100_BIN2 FROM OPENJSON(@after)) K
 WHERE EXISTS(SELECT [type],value COLLATE Latin1_General_100_BIN2 FROM OPENJSON(@before) WHERE [key] COLLATE Latin1_General_100_BIN2=K.[key]
 EXCEPT SELECT [type],value COLLATE Latin1_General_100_BIN2 FROM OPENJSON(@after) WHERE [key] COLLATE Latin1_General_100_BIN2=K.[key])
 OR EXISTS(SELECT [type],value COLLATE Latin1_General_100_BIN2 FROM OPENJSON(@after) WHERE [key] COLLATE Latin1_General_100_BIN2=K.[key]
 EXCEPT SELECT [type],value COLLATE Latin1_General_100_BIN2 FROM OPENJSON(@before) WHERE [key] COLLATE Latin1_General_100_BIN2=K.[key]);
 RETURN CASE WHEN @before IS NULL AND @after IS NULL THEN NULL ELSE N'['+COALESCE(@fields,N'')+N']' END;
END;
GO
CREATE TRIGGER dbo.MinimizeAuditInsert ON dbo.AuditLog INSTEAD OF INSERT AS
BEGIN
 SET NOCOUNT ON;
 INSERT dbo.AuditLog(CreatedUtc,Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,
 OldValues,NewValues,ChangedFields,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT CreatedUtc,Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,
 dbo.AuditSafeJson(OldValues),dbo.AuditSafeJson(NewValues),dbo.AuditChangedFields(OldValues,NewValues),
 CorrelationId,ActorAccountId,SqlPrincipal,Source FROM inserted;
END;
GO
CREATE VIEW dbo.AuditLogSafe AS
 SELECT AuditId,CreatedUtc,Actor,Action,EntityType,RecordKey,PersonId,Operation,Reason,ReasonSource,
 dbo.AuditSafeJson(OldValues) OldValues,dbo.AuditSafeJson(NewValues) NewValues,
 COALESCE(ChangedFields,dbo.AuditChangedFields(OldValues,NewValues)) ChangedFields,
 CorrelationId,ActorAccountId,SqlPrincipal,Source FROM dbo.AuditLog;
GO
-- Old processes must use the version with the safe audit read boundary.
UPDATE dbo.admin SET SessionEpoch=NEWID();
