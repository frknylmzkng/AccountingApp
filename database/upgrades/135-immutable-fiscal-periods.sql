CREATE TABLE dbo.FiscalPeriodClosings(
 ClosingId uniqueidentifier NOT NULL PRIMARY KEY,ProjectId uniqueidentifier NOT NULL REFERENCES dbo.FinanceProjects(ProjectId),
 PreviousClosingId uniqueidentifier NULL,FromDay date NOT NULL,ToDay date NOT NULL,RangeStartUtc datetime2(7) NOT NULL,RangeEndUtc datetime2(7) NOT NULL,
 ClosedUtc datetime2(7) NOT NULL,ClosedByAccountId uniqueidentifier NOT NULL,ClosedBy nvarchar(50) NOT NULL,Reason nvarchar(500) NOT NULL,
 LineCount int NOT NULL,SnapshotJson nvarchar(max) NOT NULL,SnapshotHash binary(32) NOT NULL,PayloadHash binary(32) NOT NULL,IsSealed bit NOT NULL DEFAULT 0,
 CONSTRAINT UX_FiscalProjectIdentity UNIQUE(ProjectId,ClosingId),
 CONSTRAINT FK_PreviousFiscalClosing FOREIGN KEY(ProjectId,PreviousClosingId) REFERENCES dbo.FiscalPeriodClosings(ProjectId,ClosingId),
 CONSTRAINT CK_FiscalRange CHECK(FromDay<=ToDay AND FromDay>='19000101' AND ToDay<'22000101' AND RangeStartUtc<RangeEndUtc),
 CONSTRAINT CK_FiscalCount CHECK(LineCount BETWEEN 0 AND 10000),
 CONSTRAINT CK_FiscalJson CHECK(ISJSON(SnapshotJson)=1),
 CONSTRAINT CK_FiscalHash CHECK(SnapshotHash=HASHBYTES('SHA2_256',SnapshotJson))
);
CREATE UNIQUE INDEX UX_FirstFiscalClosing ON dbo.FiscalPeriodClosings(ProjectId) WHERE PreviousClosingId IS NULL;
CREATE UNIQUE INDEX UX_NextFiscalClosing ON dbo.FiscalPeriodClosings(PreviousClosingId) WHERE PreviousClosingId IS NOT NULL;
CREATE INDEX IX_FiscalProjectHistory ON dbo.FiscalPeriodClosings(ProjectId,ToDay DESC,ClosingId);
CREATE INDEX IX_FiscalProjectCutoff ON dbo.FiscalPeriodClosings(ProjectId,RangeEndUtc) WHERE IsSealed=1;
CREATE TABLE dbo.FiscalPeriodLines(
 ClosingId uniqueidentifier NOT NULL,ProjectId uniqueidentifier NOT NULL,[LineNo] int NOT NULL,
 SourceKind nvarchar(24) NOT NULL,SourceKey nvarchar(64) NOT NULL,OccurredUtc datetime2(7) NOT NULL,
 RelatedClosingId uniqueidentifier NULL,RelatedLineNo int NULL,PayloadJson nvarchar(max) NOT NULL,PayloadHash binary(32) NOT NULL,
 CONSTRAINT PK_FiscalPeriodLines PRIMARY KEY(ClosingId,[LineNo]),
 CONSTRAINT FK_FiscalLineClosing FOREIGN KEY(ProjectId,ClosingId) REFERENCES dbo.FiscalPeriodClosings(ProjectId,ClosingId),
 CONSTRAINT FK_FiscalRelatedClosing FOREIGN KEY(ProjectId,RelatedClosingId) REFERENCES dbo.FiscalPeriodClosings(ProjectId,ClosingId),
 CONSTRAINT FK_FiscalRelatedLine FOREIGN KEY(RelatedClosingId,RelatedLineNo) REFERENCES dbo.FiscalPeriodLines(ClosingId,[LineNo]),
 CONSTRAINT UX_FiscalSourceClaim UNIQUE(ProjectId,SourceKind,SourceKey),
 CONSTRAINT CK_FiscalLineNumber CHECK([LineNo] BETWEEN 1 AND 10000),
 CONSTRAINT CK_FiscalLineRelated CHECK((RelatedClosingId IS NULL AND RelatedLineNo IS NULL) OR(RelatedClosingId IS NOT NULL AND RelatedClosingId<>ClosingId AND (RelatedLineNo IS NULL OR RelatedLineNo>0))),
 CONSTRAINT CK_FiscalLineJson CHECK(ISJSON(PayloadJson)=1),
 CONSTRAINT CK_FiscalLineHash CHECK(PayloadHash=HASHBYTES('SHA2_256',PayloadJson))
);
CREATE INDEX IX_FiscalClaimLookup ON dbo.FiscalPeriodLines(SourceKind,SourceKey) INCLUDE(ClosingId,ProjectId);
GO
-- Recording timestamps, not work/service dates. Reversals remain separate events;
-- current effective views would otherwise rewrite a historical period.
CREATE VIEW dbo.FiscalSourceEvents AS
 SELECT F.ProjectId,CONVERT(nvarchar(24),N'FinancePosted') SourceKind,CONVERT(nvarchar(64),F.EntryId) SourceKey,F.CreatedUtc OccurredUtc,F.PersonId,F.Reference,
 CONVERT(nvarchar(24),NULL) OriginKind,CONVERT(nvarchar(64),NULL) OriginKey,F.PeriodEnd ServiceDay,
 CONVERT(decimal(18,2),CASE WHEN F.Kind=N'Accrual' THEN F.Amount ELSE 0 END) Labor,
 CONVERT(decimal(18,2),CASE WHEN F.Kind=N'Expense' THEN F.Amount ELSE 0 END) OtherCost,
 CONVERT(decimal(18,2),0) CashOut,CONVERT(decimal(18,2),0) CashReturn,CONVERT(decimal(18,2),0) AdvanceOut,CONVERT(decimal(18,2),0) AdvanceReturn,
 CONVERT(decimal(18,2),0) Allocated,CONVERT(decimal(18,2),0) BankMatched FROM dbo.FinanceEntries F
 UNION ALL SELECT F.ProjectId,N'FinanceReversed',CONVERT(nvarchar(64),F.EntryId),F.ReversedUtc,F.PersonId,F.Reference,N'FinancePosted',CONVERT(nvarchar(64),F.EntryId),F.PeriodEnd,
 CASE WHEN F.Kind=N'Accrual' THEN -F.Amount ELSE 0 END,CASE WHEN F.Kind=N'Expense' THEN -F.Amount ELSE 0 END,0,0,0,0,0,0 FROM dbo.FinanceEntries F WHERE F.ReversedUtc IS NOT NULL
 UNION ALL SELECT F.ProjectId,N'AccrualCorrection',CONVERT(nvarchar(64),T.TransferId),T.PostedUtc,F.PersonId,F.Reference,N'FinancePosted',CONVERT(nvarchar(64),F.EntryId),F.PeriodEnd,
 R.Delta,0,0,0,0,0,0,0 FROM dbo.AccrualCorrectionTransfers T JOIN dbo.AccrualCorrections R ON R.CorrectionId=T.CorrectionId JOIN dbo.AccrualPostingItems I ON I.ItemId=T.SourceItemId JOIN dbo.FinanceEntries F ON F.EntryId=I.FinanceEntryId
 UNION ALL SELECT P.ProjectId,N'Payment',CONVERT(nvarchar(64),P.LedgerId),P.CreatedUtc,P.PersonId,CONVERT(nvarchar(50),P.RequestId),NULL,NULL,NULL,
 0,0,-P.Delta,0,CASE WHEN P.PaymentPurpose=N'Advance' THEN -P.Delta ELSE 0 END,0,0,0 FROM dbo.PaymentLedger P WHERE P.Kind=N'Payment' AND P.ProjectId IS NOT NULL
 UNION ALL SELECT P.ProjectId,N'PaymentReversal',CONVERT(nvarchar(64),R.LedgerId),R.CreatedUtc,R.PersonId,CONVERT(nvarchar(50),P.RequestId),N'Payment',CONVERT(nvarchar(64),P.LedgerId),NULL,
 0,0,0,R.Delta,0,CASE WHEN P.PaymentPurpose=N'Advance' THEN R.Delta ELSE 0 END,0,0 FROM dbo.PaymentLedger R JOIN dbo.PaymentLedger P ON P.LedgerId=R.ReversesLedgerId AND P.Kind=N'Payment' WHERE R.Kind=N'Reversal' AND P.ProjectId IS NOT NULL
 UNION ALL SELECT P.ProjectId,N'Allocation',CONVERT(nvarchar(64),A.AllocationId),A.CreatedUtc,P.PersonId,CONVERT(nvarchar(50),P.RequestId),N'Payment',CONVERT(nvarchar(64),P.LedgerId),NULL,
 0,0,0,0,0,0,A.Amount,0 FROM dbo.PaymentAllocations A JOIN dbo.PaymentLedger P ON P.LedgerId=A.PaymentLedgerId WHERE P.ProjectId IS NOT NULL
 UNION ALL SELECT P.ProjectId,N'AllocationRelease',CONVERT(nvarchar(64),R.ReleaseId),R.ReleasedUtc,P.PersonId,CONVERT(nvarchar(50),P.RequestId),N'Allocation',CONVERT(nvarchar(64),A.AllocationId),NULL,
 0,0,0,0,0,0,-A.Amount,0 FROM dbo.PaymentAllocationReleases R JOIN dbo.PaymentAllocations A ON A.AllocationId=R.AllocationId JOIN dbo.PaymentLedger P ON P.LedgerId=A.PaymentLedgerId WHERE P.ProjectId IS NOT NULL
 UNION ALL SELECT P.ProjectId,N'BankMatch',CONVERT(nvarchar(64),M.MatchId),M.CreatedUtc,P.PersonId,CONVERT(nvarchar(50),P.RequestId),N'Payment',CONVERT(nvarchar(64),P.LedgerId),NULL,
 0,0,0,0,0,0,0,-P.Delta FROM dbo.BankPaymentMatches M JOIN dbo.PaymentLedger P ON P.LedgerId=M.LedgerId WHERE P.ProjectId IS NOT NULL
 UNION ALL SELECT P.ProjectId,N'BankMatchRelease',CONVERT(nvarchar(64),M.ReleaseId),M.ReleasedUtc,P.PersonId,CONVERT(nvarchar(50),P.RequestId),N'BankMatch',CONVERT(nvarchar(64),M.MatchId),NULL,
 0,0,0,0,0,0,0,P.Delta FROM dbo.BankPaymentMatches M JOIN dbo.PaymentLedger P ON P.LedgerId=M.LedgerId WHERE P.ProjectId IS NOT NULL AND M.ReleasedUtc IS NOT NULL;
GO
CREATE TRIGGER dbo.GuardFiscalLines ON dbo.FiscalPeriodLines AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51320,N'Kesin kapanış satırları değişmez veya silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN dbo.FiscalPeriodClosings H ON H.ClosingId=I.ClosingId LEFT JOIN dbo.FiscalPeriodClosings R ON R.ClosingId=I.RelatedClosingId
 WHERE H.IsSealed=1 OR(I.RelatedClosingId IS NOT NULL AND (R.IsSealed=0 OR R.ToDay>=H.FromDay)))
 THROW 51321,N'Satırlar yalnız yeni kapanışa ve aynı projenin önceki kesin kayıtlarına bağlanabilir.',1;
END;
GO
CREATE TRIGGER dbo.GuardFiscalClosing ON dbo.FiscalPeriodClosings AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.ClosingId=D.ClosingId WHERE I.ClosingId IS NULL OR D.IsSealed=1 OR I.IsSealed<>1)
 THROW 51322,N'Mali dönem kapanışı değiştirilemez, yeniden açılamaz veya silinemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.ClosingId=I.ClosingId WHERE EXISTS(
 SELECT I.ProjectId,I.PreviousClosingId,I.FromDay,I.ToDay,I.RangeStartUtc,I.RangeEndUtc,I.ClosedUtc,I.ClosedByAccountId,I.ClosedBy,I.Reason,I.LineCount,I.SnapshotJson,I.SnapshotHash,I.PayloadHash EXCEPT
 SELECT D.ProjectId,D.PreviousClosingId,D.FromDay,D.ToDay,D.RangeStartUtc,D.RangeEndUtc,D.ClosedUtc,D.ClosedByAccountId,D.ClosedBy,D.Reason,D.LineCount,D.SnapshotJson,D.SnapshotHash,D.PayloadHash))
 THROW 51323,N'Kapanış gövdesi ilk kayıttan sonra değişmez.',1;
 IF EXISTS(SELECT 1 FROM inserted I LEFT JOIN deleted D ON D.ClosingId=I.ClosingId JOIN dbo.FinanceProjects P WITH(UPDLOCK,HOLDLOCK) ON P.ProjectId=I.ProjectId
 OUTER APPLY(SELECT TOP(1) H.ClosingId,H.ToDay FROM dbo.FiscalPeriodClosings H WITH(UPDLOCK,HOLDLOCK) WHERE H.ProjectId=I.ProjectId AND H.ClosingId<>I.ClosingId AND H.IsSealed=1 ORDER BY H.ToDay DESC) L
 WHERE D.ClosingId IS NULL AND(I.IsSealed<>0 OR EXISTS(SELECT I.PreviousClosingId EXCEPT SELECT L.ClosingId)
 OR(L.ClosingId IS NOT NULL AND I.FromDay<>DATEADD(day,1,L.ToDay)) OR I.RangeEndUtc>SYSUTCDATETIME()
 OR I.RangeStartUtc<>CONVERT(datetime2,CONVERT(datetime2,I.FromDay) AT TIME ZONE 'Turkey Standard Time' AT TIME ZONE 'UTC')
 OR I.RangeEndUtc<>CONVERT(datetime2,CONVERT(datetime2,DATEADD(day,1,I.ToDay)) AT TIME ZONE 'Turkey Standard Time' AT TIME ZONE 'UTC')))
 THROW 51324,N'Dönem tamamlanmış olmalı ve son kapanışı boşluksuz izlemeli; güncel önizleme oluşturun.',1;
END;
GO
UPDATE dbo.admin SET SessionEpoch=NEWID();

GO
CREATE TRIGGER dbo.SealFiscalClosing ON dbo.FiscalPeriodClosings AFTER UPDATE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted I WHERE I.IsSealed=1 AND(
 ISNULL(TRY_CONVERT(int,JSON_VALUE(I.SnapshotJson,'$.Format')),0)<>1
 OR ISNULL(JSON_VALUE(I.SnapshotJson,'$.ClosingId'),N'')<>CONVERT(nvarchar(36),I.ClosingId)
 OR ISNULL(JSON_VALUE(I.SnapshotJson,'$.ProjectId'),N'')<>CONVERT(nvarchar(36),I.ProjectId)
 OR ISNULL(JSON_VALUE(I.SnapshotJson,'$.TimeZoneId'),N'')<>N'Europe/Istanbul'
 OR JSON_QUERY(I.SnapshotJson,'$.Lines') IS NULL
 OR I.LineCount<>(SELECT COUNT(*) FROM OPENJSON(I.SnapshotJson,'$.Lines'))
 OR I.LineCount<>(SELECT COUNT(*) FROM dbo.FiscalPeriodLines L WHERE L.ClosingId=I.ClosingId)
 OR EXISTS(SELECT L.[LineNo],L.ProjectId,L.SourceKind,L.SourceKey,L.OccurredUtc,L.RelatedClosingId,L.RelatedLineNo,L.PayloadHash FROM dbo.FiscalPeriodLines L WHERE L.ClosingId=I.ClosingId EXCEPT SELECT CONVERT(int,J.[key])+1,I.ProjectId,CONVERT(nvarchar(24),JSON_VALUE(J.value,'$.SourceKind')),CONVERT(nvarchar(64),JSON_VALUE(J.value,'$.SourceKey')),CONVERT(datetime2(7),JSON_VALUE(J.value,'$.OccurredUtc'),127),TRY_CONVERT(uniqueidentifier,JSON_VALUE(J.value,'$.RelatedClosingId')),TRY_CONVERT(int,JSON_VALUE(J.value,'$.RelatedLineNo')),HASHBYTES('SHA2_256',J.value) FROM OPENJSON(I.SnapshotJson,'$.Lines') J) OR EXISTS(SELECT CONVERT(int,J.[key])+1,I.ProjectId,CONVERT(nvarchar(24),JSON_VALUE(J.value,'$.SourceKind')),CONVERT(nvarchar(64),JSON_VALUE(J.value,'$.SourceKey')),CONVERT(datetime2(7),JSON_VALUE(J.value,'$.OccurredUtc'),127),TRY_CONVERT(uniqueidentifier,JSON_VALUE(J.value,'$.RelatedClosingId')),TRY_CONVERT(int,JSON_VALUE(J.value,'$.RelatedLineNo')),HASHBYTES('SHA2_256',J.value) FROM OPENJSON(I.SnapshotJson,'$.Lines') J EXCEPT SELECT L.[LineNo],L.ProjectId,L.SourceKind,L.SourceKey,L.OccurredUtc,L.RelatedClosingId,L.RelatedLineNo,L.PayloadHash FROM dbo.FiscalPeriodLines L WHERE L.ClosingId=I.ClosingId)))
 THROW 51325,N'Kapanışın kaynak satırları değişmez içerikle uzlaşmıyor; kesinleştirme geri alındı.',1;
END;
GO
CREATE TRIGGER dbo.Audit_FiscalClosing ON dbo.FiscalPeriodClosings AFTER UPDATE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT I.ClosedBy,N'FiscalPeriod.Finalize',N'FiscalPeriodClosing',CONVERT(nvarchar(100),I.ClosingId),N'Seal',I.Reason,N'UserProvided',
 (SELECT I.ProjectId,I.PreviousClosingId,I.FromDay,I.ToDay,I.ClosedUtc,I.LineCount,I.SnapshotHash FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER),
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),I.ClosedByAccountId,ORIGINAL_LOGIN(),N'Application' FROM inserted I JOIN deleted D ON D.ClosingId=I.ClosingId WHERE I.IsSealed=1 AND D.IsSealed=0;
END;

GO
CREATE TRIGGER dbo.ProtectFiscalFinanceDate ON dbo.FinanceEntries AFTER INSERT,UPDATE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted I LEFT JOIN deleted D ON D.EntryId=I.EntryId
 JOIN dbo.FiscalPeriodClosings H WITH(HOLDLOCK) ON H.ProjectId=I.ProjectId AND H.IsSealed=1 WHERE CASE WHEN D.EntryId IS NULL THEN I.CreatedUtc WHEN D.ReversedUtc IS NULL THEN I.ReversedUtc ELSE NULL END<H.RangeEndUtc)
 THROW 51326,N'Kapalı mali döneme kayıt zamanı geri alınamaz; sonraki açık dönemde bağlantılı işlem kullanın.',1;
END;

GO
CREATE TRIGGER dbo.ProtectFiscalPaymentDate ON dbo.PaymentLedger AFTER INSERT,UPDATE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted I LEFT JOIN deleted D ON D.LedgerId=I.LedgerId LEFT JOIN dbo.PaymentLedger P ON P.LedgerId=I.ReversesLedgerId
 JOIN dbo.FiscalPeriodClosings H WITH(HOLDLOCK) ON H.ProjectId=CASE WHEN I.Kind=N'Reversal' THEN P.ProjectId ELSE I.ProjectId END AND H.IsSealed=1 WHERE I.CreatedUtc<H.RangeEndUtc AND D.LedgerId IS NULL AND I.Kind IN(N'Payment',N'Reversal'))
 THROW 51326,N'Kapalı mali döneme kayıt zamanı geri alınamaz; sonraki açık dönemde bağlantılı işlem kullanın.',1;
END;

GO
CREATE TRIGGER dbo.ProtectFiscalCorrectionDate ON dbo.AccrualCorrectionTransfers AFTER INSERT,UPDATE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted I  JOIN dbo.AccrualPostingItems Q ON Q.ItemId=I.SourceItemId JOIN dbo.FinanceEntries F ON F.EntryId=Q.FinanceEntryId
 JOIN dbo.FiscalPeriodClosings H WITH(HOLDLOCK) ON H.ProjectId=F.ProjectId AND H.IsSealed=1 WHERE I.PostedUtc<H.RangeEndUtc)
 THROW 51326,N'Kapalı mali döneme kayıt zamanı geri alınamaz; sonraki açık dönemde bağlantılı işlem kullanın.',1;
END;

GO
CREATE TRIGGER dbo.ProtectFiscalAllocationDate ON dbo.PaymentAllocations AFTER INSERT,UPDATE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted I  JOIN dbo.PaymentLedger P ON P.LedgerId=I.PaymentLedgerId
 JOIN dbo.FiscalPeriodClosings H WITH(HOLDLOCK) ON H.ProjectId=P.ProjectId AND H.IsSealed=1 WHERE I.CreatedUtc<H.RangeEndUtc)
 THROW 51326,N'Kapalı mali döneme kayıt zamanı geri alınamaz; sonraki açık dönemde bağlantılı işlem kullanın.',1;
END;

GO
CREATE TRIGGER dbo.ProtectFiscalAllocationReleaseDate ON dbo.PaymentAllocationReleases AFTER INSERT,UPDATE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted I  JOIN dbo.PaymentAllocations A ON A.AllocationId=I.AllocationId JOIN dbo.PaymentLedger P ON P.LedgerId=A.PaymentLedgerId
 JOIN dbo.FiscalPeriodClosings H WITH(HOLDLOCK) ON H.ProjectId=P.ProjectId AND H.IsSealed=1 WHERE I.ReleasedUtc<H.RangeEndUtc)
 THROW 51326,N'Kapalı mali döneme kayıt zamanı geri alınamaz; sonraki açık dönemde bağlantılı işlem kullanın.',1;
END;

GO
CREATE TRIGGER dbo.ProtectFiscalBankMatchDate ON dbo.BankPaymentMatches AFTER INSERT,UPDATE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted I LEFT JOIN deleted D ON D.MatchId=I.MatchId JOIN dbo.PaymentLedger P ON P.LedgerId=I.LedgerId
 JOIN dbo.FiscalPeriodClosings H WITH(HOLDLOCK) ON H.ProjectId=P.ProjectId AND H.IsSealed=1 WHERE CASE WHEN D.MatchId IS NULL THEN I.CreatedUtc WHEN D.ReleasedUtc IS NULL THEN I.ReleasedUtc ELSE NULL END<H.RangeEndUtc)
 THROW 51326,N'Kapalı mali döneme kayıt zamanı geri alınamaz; sonraki açık dönemde bağlantılı işlem kullanın.',1;
END;

GO
CREATE TRIGGER dbo.ProtectFiscalPaymentBody ON dbo.PaymentLedger AFTER UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D JOIN dbo.FiscalPeriodLines L ON L.SourceKind IN(N'Payment',N'PaymentReversal') AND L.SourceKey=CONVERT(nvarchar(64),D.LedgerId)
 JOIN dbo.FiscalPeriodClosings H ON H.ClosingId=L.ClosingId AND H.IsSealed=1 LEFT JOIN inserted I ON I.LedgerId=D.LedgerId
 WHERE I.LedgerId IS NULL OR EXISTS(SELECT I.[LedgerId],I.[TC],I.[PersonnelGroup],I.[CreatedUtc],I.[Delta],I.[Kind],I.[Actor],I.[RequestId],I.[PersonId],I.[PaymentPurpose],I.[ProjectId],I.[FinanceEntryId],I.[ReversesLedgerId],I.[AccrualPostingId],I.[AccrualCorrectionId] EXCEPT SELECT D.[LedgerId],D.[TC],D.[PersonnelGroup],D.[CreatedUtc],D.[Delta],D.[Kind],D.[Actor],D.[RequestId],D.[PersonId],D.[PaymentPurpose],D.[ProjectId],D.[FinanceEntryId],D.[ReversesLedgerId],D.[AccrualPostingId],D.[AccrualCorrectionId])
 OR(D.ReversedUtc IS NOT NULL AND(I.ReversedUtc IS NULL OR I.ReversedUtc<>D.ReversedUtc))
 OR(D.Kind<>N'Payment' AND EXISTS(SELECT I.ReversedUtc EXCEPT SELECT D.ReversedUtc))
 OR(D.ReversedUtc IS NULL AND I.ReversedUtc<H.ClosedUtc))
 THROW 51327,N'Kapalı ödeme gövdesi değişmez; yalnız sonraki dönemde kaynak bağlantılı iptal yapılabilir.',1;
END;
