-- Immutable snapshots for completed business days; no historic closing is invented.
ALTER TABLE dbo.PaymentLedger ADD ReversesLedgerId bigint NULL REFERENCES dbo.PaymentLedger(LedgerId);
GO
ALTER TABLE dbo.PaymentLedger ADD CONSTRAINT CK_ReversalLink CHECK(ReversesLedgerId IS NULL OR Kind=N'Reversal');
CREATE UNIQUE INDEX UX_Ledger_ReversalLink ON dbo.PaymentLedger(ReversesLedgerId) WHERE ReversesLedgerId IS NOT NULL;
CREATE INDEX IX_Ledger_CreatedUtc ON dbo.PaymentLedger(CreatedUtc,LedgerId) INCLUDE(Kind,Delta);
CREATE TABLE dbo.DailyClosings(
 ClosingId uniqueidentifier NOT NULL CONSTRAINT PK_DailyClosings PRIMARY KEY,
 BusinessDate date NOT NULL CONSTRAINT UX_DailyClosingDate UNIQUE,
 TimeZoneId nvarchar(64) NOT NULL, RangeStartUtc datetime2 NOT NULL, RangeEndUtc datetime2 NOT NULL,
 ClosedUtc datetime2 NOT NULL, Actor nvarchar(50) NOT NULL, ActorAccountId uniqueidentifier NOT NULL, Reason nvarchar(500) NOT NULL,
 PaymentCount bigint NOT NULL, ReversalCount bigint NOT NULL,
 CashOut decimal(28,2) NOT NULL, CashReturn decimal(28,2) NOT NULL, NetCash decimal(28,2) NOT NULL,
 DeclaredCash decimal(28,2) NOT NULL, Difference decimal(28,2) NOT NULL,
 SnapshotJson nvarchar(max) NOT NULL, SnapshotHash binary(32) NOT NULL,
 CONSTRAINT CK_ClosingRange CHECK(RangeEndUtc>RangeStartUtc),
 CONSTRAINT CK_ClosingAmounts CHECK(PaymentCount>=0 AND ReversalCount>=0 AND CashOut>=0 AND CashReturn>=0
  AND NetCash=CashOut-CashReturn AND Difference=DeclaredCash-NetCash),
 CONSTRAINT CK_ClosingJson CHECK(ISJSON(SnapshotJson)=1),
 CONSTRAINT CK_ClosingHash CHECK(SnapshotHash=HASHBYTES('SHA2_256',SnapshotJson)));
CREATE INDEX IX_DailyClosingRange ON dbo.DailyClosings(RangeStartUtc,RangeEndUtc);
-- Running old clients must not create unlinked reversals under the new policy.
UPDATE dbo.admin SET SessionEpoch=NEWID();
GO
CREATE TRIGGER dbo.ProtectDailyClosings ON dbo.DailyClosings INSTEAD OF UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51055,N'Kesinleşmiş günlük kapanış değiştirilemez veya silinemez.',1;
END;
GO
CREATE TRIGGER dbo.AuditChanges_DailyClosings ON dbo.DailyClosings AFTER INSERT AS
BEGIN
 SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT I.Actor,N'Closing.Finalize',N'DailyClosing',CONVERT(nvarchar(100),I.ClosingId),N'Insert',I.Reason,N'UserProvided',
 (SELECT I.BusinessDate,I.TimeZoneId,I.RangeStartUtc,I.RangeEndUtc,I.ClosedUtc,I.PaymentCount,I.ReversalCount,
 I.CashOut,I.CashReturn,I.NetCash,I.DeclaredCash,I.Difference,I.SnapshotHash FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER),
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),I.ActorAccountId,ORIGINAL_LOGIN(),
 CASE WHEN SESSION_CONTEXT(N'Audit.Actor') IS NULL THEN N'DirectSql' ELSE N'Application' END FROM inserted I;
END;
GO
CREATE TRIGGER dbo.ProtectClosedCashFlows ON dbo.PaymentLedger AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted I LEFT JOIN dbo.PaymentLedger P ON P.LedgerId=I.ReversesLedgerId
 WHERE I.ReversesLedgerId IS NOT NULL AND (P.Kind<>N'Payment' OR P.LedgerId IS NULL OR I.Kind<>N'Reversal'
 OR I.TC<>P.TC OR I.PersonnelGroup<>P.PersonnelGroup OR I.Delta<>-P.Delta
 OR ISNULL(I.PersonId,'00000000-0000-0000-0000-000000000000')<>ISNULL(P.PersonId,'00000000-0000-0000-0000-000000000000')))
 THROW 51058,N'İptal kaynağı, personel veya tutar eşleşmiyor.',1;
 IF EXISTS(
 SELECT 1 FROM inserted I FULL OUTER JOIN deleted D ON I.LedgerId=D.LedgerId
 CROSS APPLY(SELECT CASE WHEN D.LedgerId IS NULL THEN NULL ELSE (SELECT D.LedgerId,D.TC,D.PersonnelGroup,D.CreatedUtc,D.Delta,D.Kind,D.Actor,D.RequestId,D.PersonId,D.PaymentPurpose,D.ProjectId,D.FinanceEntryId,D.ReversesLedgerId FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END BeforeJson,
 CASE WHEN I.LedgerId IS NULL THEN NULL ELSE (SELECT I.LedgerId,I.TC,I.PersonnelGroup,I.CreatedUtc,I.Delta,I.Kind,I.Actor,I.RequestId,I.PersonId,I.PaymentPurpose,I.ProjectId,I.FinanceEntryId,I.ReversesLedgerId FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END AfterJson) J
 WHERE (EXISTS(SELECT 1 FROM dbo.DailyClosings C WITH(HOLDLOCK) WHERE D.Kind IN(N'Payment',N'Reversal') AND D.CreatedUtc>=C.RangeStartUtc AND D.CreatedUtc<C.RangeEndUtc)
 OR EXISTS(SELECT 1 FROM dbo.DailyClosings C WITH(HOLDLOCK) WHERE I.Kind IN(N'Payment',N'Reversal') AND I.CreatedUtc>=C.RangeStartUtc AND I.CreatedUtc<C.RangeEndUtc))
 AND (J.BeforeJson IS NULL OR J.AfterJson IS NULL OR J.BeforeJson COLLATE Latin1_General_100_BIN2<>J.AfterJson COLLATE Latin1_General_100_BIN2
 OR (D.ReversedUtc IS NOT NULL AND (I.ReversedUtc IS NULL OR I.ReversedUtc<>D.ReversedUtc))
 OR EXISTS(SELECT 1 FROM dbo.DailyClosings C WHERE D.Kind=N'Payment' AND D.CreatedUtc>=C.RangeStartUtc AND D.CreatedUtc<C.RangeEndUtc
 AND D.ReversedUtc IS NULL AND I.ReversedUtc<C.ClosedUtc)))
 THROW 51056,N'Kapalı güne nakit hareketi eklenemez veya geçmiş hareket değiştirilemez. Sonraki açık günde bağlantılı iptal kullanın.',1;
END;
GO

CREATE OR ALTER TRIGGER dbo.AuditChanges_PaymentLedger ON dbo.[PaymentLedger] AFTER INSERT,UPDATE,DELETE AS
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
 CROSS APPLY(SELECT CASE WHEN D.[LedgerId] IS NULL THEN NULL ELSE (SELECT D.[LedgerId],D.[PersonId],D.[TC],D.[PersonnelGroup],D.[Delta],D.[Kind],D.[Actor],D.[RequestId],D.[ReversedUtc],D.[PaymentPurpose],D.[ProjectId],D.[FinanceEntryId],D.[ReversesLedgerId] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END BeforeJson,
 CASE WHEN I.[LedgerId] IS NULL THEN NULL ELSE (SELECT I.[LedgerId],I.[PersonId],I.[TC],I.[PersonnelGroup],I.[Delta],I.[Kind],I.[Actor],I.[RequestId],I.[ReversedUtc],I.[PaymentPurpose],I.[ProjectId],I.[FinanceEntryId],I.[ReversesLedgerId] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER) END AfterJson) J
 WHERE J.BeforeJson IS NULL OR J.AfterJson IS NULL OR J.BeforeJson COLLATE Latin1_General_100_BIN2<>J.AfterJson COLLATE Latin1_General_100_BIN2;
END;
GO


