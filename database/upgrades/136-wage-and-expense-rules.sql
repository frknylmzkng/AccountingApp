CREATE TABLE dbo.WagePolicies(
 PolicyId uniqueidentifier NOT NULL PRIMARY KEY,ProjectId uniqueidentifier NOT NULL REFERENCES dbo.FinanceProjects(ProjectId),PreviousPolicyId uniqueidentifier NULL REFERENCES dbo.WagePolicies(PolicyId),
 RuleCode nvarchar(30) NOT NULL,Revision int NOT NULL,Name nvarchar(100) NOT NULL,Kind nvarchar(24) NOT NULL,ValidFrom date NOT NULL,ValidTo date NULL,
 UnitAmount decimal(18,2) NOT NULL,PremiumFactor decimal(9,4) NOT NULL,Divisor int NOT NULL,Currency nvarchar(3) NOT NULL DEFAULT N'TRY',
 CreatedByAccountId uniqueidentifier NOT NULL,CreatedBy nvarchar(50) NOT NULL,CreatedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),Reason nvarchar(500) NOT NULL,PayloadHash binary(32) NOT NULL,RecordVersion rowversion NOT NULL,
 CONSTRAINT UX_WagePolicyRevision UNIQUE(ProjectId,RuleCode,Revision),
 CONSTRAINT CK_WagePolicy CHECK(Revision>0 AND (ValidTo IS NULL OR ValidTo>=ValidFrom) AND Currency=N'TRY' AND(
 (Kind=N'OvertimePremium' AND UnitAmount=0 AND PremiumFactor>0 AND PremiumFactor<=5 AND Divisor=60) OR
 (Kind=N'MonthlyWage' AND UnitAmount>0 AND UnitAmount<=1000000 AND PremiumFactor=1 AND Divisor BETWEEN 1 AND 31) OR
 (Kind IN(N'DailyWage',N'ExtraPay',N'ProjectExpense') AND UnitAmount>0 AND UnitAmount<=1000000 AND PremiumFactor=1 AND Divisor=1)))
);
CREATE UNIQUE INDEX UX_WagePolicyRoot ON dbo.WagePolicies(ProjectId,RuleCode) WHERE PreviousPolicyId IS NULL;
CREATE UNIQUE INDEX UX_WagePolicyNext ON dbo.WagePolicies(PreviousPolicyId) WHERE PreviousPolicyId IS NOT NULL;
CREATE TABLE dbo.WagePolicyDecisions(
 DecisionId uniqueidentifier NOT NULL PRIMARY KEY,PolicyId uniqueidentifier NOT NULL UNIQUE REFERENCES dbo.WagePolicies(PolicyId),Kind nvarchar(12) NOT NULL CHECK(Kind IN(N'Approve',N'Reject',N'Cancel')),
 ActorAccountId uniqueidentifier NOT NULL,Actor nvarchar(50) NOT NULL,CreatedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),Reason nvarchar(500) NOT NULL,PayloadHash binary(32) NOT NULL
);
CREATE TABLE dbo.WageClaims(
 ClaimId uniqueidentifier NOT NULL PRIMARY KEY,ProjectId uniqueidentifier NOT NULL REFERENCES dbo.FinanceProjects(ProjectId),PolicyId uniqueidentifier NOT NULL REFERENCES dbo.WagePolicies(PolicyId),Kind nvarchar(24) NOT NULL,
 EmploymentId uniqueidentifier NULL REFERENCES dbo.EmploymentPeriods(EmploymentId),PersonId uniqueidentifier NULL REFERENCES dbo.PersonnelIdentities(PersonId),PersonnelGroup int NULL,
 FromDay date NOT NULL,ToDay date NOT NULL,DocumentKey nvarchar(100) NOT NULL,PremiumLineId uniqueidentifier NULL REFERENCES dbo.AccrualCalculationLines(LineId),
 Units decimal(18,4) NOT NULL,UnitAmount decimal(18,2) NOT NULL,PremiumFactor decimal(9,4) NOT NULL,Divisor int NOT NULL,Amount decimal(18,2) NOT NULL,
 SnapshotJson nvarchar(max) NOT NULL,SnapshotHash binary(32) NOT NULL,CreatedByAccountId uniqueidentifier NOT NULL,CreatedBy nvarchar(50) NOT NULL,CreatedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),Reason nvarchar(500) NOT NULL,PayloadHash binary(32) NOT NULL,
 IsReserved bit NOT NULL DEFAULT 1,ReleasedUtc datetime2(7) NULL,RecordVersion rowversion NOT NULL,
 WageMonth AS DATEFROMPARTS(YEAR(FromDay),MONTH(FromDay),1) PERSISTED,
 CONSTRAINT CK_WageClaimPeriod CHECK(ToDay>=FromDay AND DATEDIFF(day,FromDay,ToDay)<=365),
 CONSTRAINT CK_WageClaimAmount CHECK(Units>0 AND Units<=1000000 AND UnitAmount>0 AND PremiumFactor>0 AND Divisor>0 AND Amount>0),
 CONSTRAINT CK_WageClaimPerson CHECK((Kind=N'ProjectExpense' AND PersonId IS NULL AND EmploymentId IS NULL AND PersonnelGroup IS NULL AND PremiumLineId IS NULL) OR(Kind IN(N'OvertimePremium',N'DailyWage',N'MonthlyWage',N'ExtraPay') AND PersonId IS NOT NULL AND EmploymentId IS NOT NULL AND PersonnelGroup IN(0,1))),
 CONSTRAINT CK_WageClaimRelease CHECK((IsReserved=1 AND ReleasedUtc IS NULL) OR(IsReserved=0 AND ReleasedUtc IS NOT NULL)),
 CONSTRAINT CK_WageClaimJson CHECK(ISJSON(SnapshotJson)=1 AND SnapshotHash=HASHBYTES('SHA2_256',SnapshotJson))
);
CREATE UNIQUE INDEX UX_WageDocument ON dbo.WageClaims(ProjectId,DocumentKey) WHERE IsReserved=1;
CREATE UNIQUE INDEX UX_WagePremiumSource ON dbo.WageClaims(PremiumLineId) WHERE IsReserved=1 AND PremiumLineId IS NOT NULL;
CREATE UNIQUE INDEX UX_WageMonthlyBase ON dbo.WageClaims(EmploymentId,WageMonth) WHERE IsReserved=1 AND Kind=N'MonthlyWage';
CREATE INDEX IX_WageBaseCoverage ON dbo.WageClaims(EmploymentId,FromDay,ToDay) INCLUDE(Kind,IsReserved);
CREATE INDEX IX_WageClaimHistory ON dbo.WageClaims(ProjectId,CreatedUtc DESC,ClaimId);
CREATE TABLE dbo.WageClaimActions(
 ActionId uniqueidentifier NOT NULL PRIMARY KEY,ClaimId uniqueidentifier NOT NULL REFERENCES dbo.WageClaims(ClaimId),Kind nvarchar(20) NOT NULL CHECK(Kind IN(N'Approve',N'Reject',N'Cancel',N'RequestReverse',N'ApproveReverse')),
 ActorAccountId uniqueidentifier NOT NULL,Actor nvarchar(50) NOT NULL,CreatedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),Reason nvarchar(500) NOT NULL,PayloadHash binary(32) NOT NULL,
 CONSTRAINT UX_WageAction UNIQUE(ClaimId,Kind)
);
CREATE TABLE dbo.WageClaimTransfers(
 TransferId uniqueidentifier NOT NULL PRIMARY KEY,ClaimId uniqueidentifier NOT NULL REFERENCES dbo.WageClaims(ClaimId),Kind nvarchar(10) NOT NULL CHECK(Kind IN(N'Post',N'Reverse')),
 FinanceEntryId uniqueidentifier NOT NULL REFERENCES dbo.FinanceEntries(EntryId),LedgerId bigint NULL REFERENCES dbo.PaymentLedger(LedgerId),Amount decimal(18,2) NOT NULL,
 BeforeBalance decimal(18,2) NULL,AfterBalance decimal(18,2) NULL,ActorAccountId uniqueidentifier NOT NULL,Actor nvarchar(50) NOT NULL,CreatedUtc datetime2(7) NOT NULL DEFAULT SYSUTCDATETIME(),Reason nvarchar(500) NOT NULL,PayloadHash binary(32) NOT NULL,
 CONSTRAINT UX_WageTransfer UNIQUE(ClaimId,Kind)
);
ALTER TABLE dbo.FinanceEntries ADD WageClaimId uniqueidentifier NULL REFERENCES dbo.WageClaims(ClaimId);
ALTER TABLE dbo.PaymentLedger ADD WageClaimId uniqueidentifier NULL REFERENCES dbo.WageClaims(ClaimId),WageTransferKind nvarchar(10) NULL;
GO
CREATE UNIQUE INDEX UX_WageFinance ON dbo.FinanceEntries(WageClaimId) WHERE WageClaimId IS NOT NULL;
CREATE UNIQUE INDEX UX_WageLedger ON dbo.PaymentLedger(WageClaimId,WageTransferKind) WHERE WageClaimId IS NOT NULL;
ALTER TABLE dbo.PaymentLedger ADD CONSTRAINT CK_WageLedger CHECK((WageClaimId IS NULL AND WageTransferKind IS NULL) OR(WageClaimId IS NOT NULL AND WageTransferKind IN(N'Post',N'Reverse') AND Kind=N'Allocation' AND RequestId IS NULL AND ReversedUtc IS NULL));
GO
CREATE TRIGGER dbo.GuardWagePolicies ON dbo.WagePolicies AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51340,N'Ücret/gider kuralı değişmez; gerekçeli yeni sürüm oluşturun.',1;
 IF EXISTS(SELECT 1 FROM inserted I LEFT JOIN dbo.WagePolicies P ON P.PolicyId=I.PreviousPolicyId WHERE
 (I.PreviousPolicyId IS NULL AND I.Revision<>1) OR(I.PreviousPolicyId IS NOT NULL AND(P.ProjectId<>I.ProjectId OR P.RuleCode<>I.RuleCode OR P.Kind<>I.Kind OR I.Revision<>P.Revision+1 OR NOT EXISTS(SELECT 1 FROM dbo.WagePolicyDecisions D WHERE D.PolicyId=P.PolicyId))))
 THROW 51341,N'Yeni kural sürümü aynı proje/kod/türdeki karara bağlanmış önceki sürümü izlemeli.',1;
END;
GO
CREATE TRIGGER dbo.GuardWagePolicyDecisions ON dbo.WagePolicyDecisions AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51342,N'Kural kararı değişmez.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN dbo.WagePolicies P ON P.PolicyId=I.PolicyId WHERE I.Kind IN(N'Approve',N'Reject') AND dbo.SameAccountPerson(I.ActorAccountId,P.CreatedByAccountId)=1)
 THROW 51343,N'Kuralı hazırlayanla aynı kişi onay/ret veremez.',1;
END;
GO
CREATE TRIGGER dbo.GuardWageClaims ON dbo.WageClaims AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.ClaimId=D.ClaimId WHERE I.ClaimId IS NULL OR D.IsReserved=0 OR I.IsReserved<>0 OR I.ReleasedUtc IS NULL)
 THROW 51344,N'Ücret/gider talebi değişmez; yalnız kayıtlı iptal/ret veya ters aktarım rezervasyonu kaldırabilir.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.ClaimId=I.ClaimId WHERE EXISTS(
 SELECT I.ProjectId,I.PolicyId,I.Kind,I.EmploymentId,I.PersonId,I.PersonnelGroup,I.FromDay,I.ToDay,I.DocumentKey,I.PremiumLineId,I.Units,I.UnitAmount,I.PremiumFactor,I.Divisor,I.Amount,I.SnapshotJson,I.SnapshotHash,I.CreatedByAccountId,I.CreatedBy,I.CreatedUtc,I.Reason,I.PayloadHash EXCEPT
 SELECT D.ProjectId,D.PolicyId,D.Kind,D.EmploymentId,D.PersonId,D.PersonnelGroup,D.FromDay,D.ToDay,D.DocumentKey,D.PremiumLineId,D.Units,D.UnitAmount,D.PremiumFactor,D.Divisor,D.Amount,D.SnapshotJson,D.SnapshotHash,D.CreatedByAccountId,D.CreatedBy,D.CreatedUtc,D.Reason,D.PayloadHash)) THROW 51345,N'Talebin ilk hesap ve kaynak değerleri değiştirilemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.ClaimId=I.ClaimId WHERE NOT EXISTS(SELECT 1 FROM dbo.WageClaimActions A WHERE A.ClaimId=I.ClaimId AND A.Kind IN(N'Reject',N'Cancel')) AND NOT EXISTS(SELECT 1 FROM dbo.WageClaimTransfers T WHERE T.ClaimId=I.ClaimId AND T.Kind=N'Reverse')) THROW 51346,N'Rezervasyon kaldırma kanıtı eksik.',1;
 IF EXISTS(SELECT 1 FROM inserted I LEFT JOIN deleted D ON D.ClaimId=I.ClaimId JOIN dbo.WagePolicies P ON P.PolicyId=I.PolicyId WHERE D.ClaimId IS NULL AND(I.IsReserved<>1 OR I.Kind<>P.Kind OR P.ProjectId<>I.ProjectId OR P.ValidFrom>I.FromDay OR(P.ValidTo IS NOT NULL AND P.ValidTo<I.ToDay)
 OR NOT EXISTS(SELECT 1 FROM dbo.WagePolicyDecisions X WHERE X.PolicyId=P.PolicyId AND X.Kind=N'Approve')
 OR EXISTS(SELECT 1 FROM dbo.WagePolicies N JOIN dbo.WagePolicyDecisions X ON X.PolicyId=N.PolicyId AND X.Kind=N'Approve' WHERE N.ProjectId=P.ProjectId AND N.RuleCode=P.RuleCode AND N.Revision>P.Revision AND N.ValidFrom<=I.ToDay AND(N.ValidTo IS NULL OR N.ValidTo>=I.FromDay)))) THROW 51347,N'Talep güncel, tarih aralığını kapsayan onaylı kural sürümünü kullanmalı.',1;
 IF EXISTS(SELECT 1 FROM inserted I LEFT JOIN deleted D ON D.ClaimId=I.ClaimId WHERE D.ClaimId IS NULL AND I.Kind IN(N'DailyWage',N'MonthlyWage') AND(
 EXISTS(SELECT 1 FROM dbo.AccrualCalculationLines L WITH(UPDLOCK,HOLDLOCK) WHERE L.EmploymentId=I.EmploymentId AND L.WorkDate BETWEEN I.FromDay AND I.ToDay)
 OR EXISTS(SELECT 1 FROM dbo.WageClaims C WITH(UPDLOCK,HOLDLOCK) WHERE C.ClaimId<>I.ClaimId AND C.IsReserved=1 AND C.Kind IN(N'DailyWage',N'MonthlyWage') AND C.EmploymentId=I.EmploymentId AND C.FromDay<=I.ToDay AND C.ToDay>=I.FromDay)))
 THROW 51348,N'Aynı çalışma döneminin temel ücreti saatlik/günlük/aylık olarak iki kez hesaplanamaz.',1;
END;
GO
CREATE TRIGGER dbo.GuardWageAgainstHourly ON dbo.AccrualCalculationLines AFTER INSERT AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted L JOIN dbo.WageClaims C WITH(UPDLOCK,HOLDLOCK) ON C.EmploymentId=L.EmploymentId AND C.IsReserved=1 AND C.Kind IN(N'DailyWage',N'MonthlyWage') AND L.WorkDate BETWEEN C.FromDay AND C.ToDay)
 THROW 51349,N'Kaynak günü günlük/aylık temel ücret kapsamına alınmış; saatlik hesap çoğaltılamaz.',1;
END;
GO
CREATE TRIGGER dbo.GuardPremiumBaseCorrection ON dbo.AccrualCorrectionTransfers AFTER INSERT AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted T JOIN dbo.AccrualPostingItems I ON I.ItemId=T.SourceItemId JOIN dbo.AccrualPostings P ON P.PostingId=I.PostingId JOIN dbo.AccrualCalculationLines L ON L.CalculationId=P.CalculationId AND L.EmploymentId=I.EmploymentId JOIN dbo.WageClaims C ON C.PremiumLineId=L.LineId AND C.IsReserved=1)
 THROW 51350,N'Önce bu hakedişe bağlı ek fazla mesai talebini iptal/ters aktarım ile sonuçlandırın.',1;
END;
GO
UPDATE dbo.admin SET SessionEpoch=NEWID();

GO
CREATE TRIGGER dbo.GuardWageClaimValues ON dbo.WageClaims AFTER INSERT AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted I JOIN dbo.WagePolicies P ON P.PolicyId=I.PolicyId WHERE I.PremiumFactor<>P.PremiumFactor OR I.Divisor<>P.Divisor
 OR(I.Kind<>N'OvertimePremium' AND(I.UnitAmount<>P.UnitAmount OR I.PremiumLineId IS NOT NULL))
 OR I.Amount<>ROUND(CONVERT(decimal(12,4),I.Units)*CONVERT(decimal(9,2),I.UnitAmount)*CONVERT(decimal(5,4),I.PremiumFactor)/CONVERT(decimal(2,0),I.Divisor),2)
 OR(I.Kind=N'DailyWage' AND(I.FromDay<>I.ToDay OR I.Units<>1))
 OR(I.Kind=N'MonthlyWage' AND(YEAR(I.FromDay)<>YEAR(I.ToDay) OR MONTH(I.FromDay)<>MONTH(I.ToDay) OR I.Units>I.Divisor OR((DAY(I.FromDay)<>1 OR I.ToDay<>EOMONTH(I.ToDay)) AND I.Units>DATEDIFF(day,I.FromDay,I.ToDay)+1))))
 THROW 51351,N'Talep birim, bölen ve yuvarlama hesabıyla uzlaşmıyor.',1;
 IF EXISTS(SELECT 1 FROM inserted I WHERE I.PersonId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.EmploymentPeriods E JOIN(SELECT PersonId,CurrentEmploymentId,ArchivedUtc,0 PersonnelGroup FROM dbo.Calisan UNION ALL SELECT PersonId,CurrentEmploymentId,ArchivedUtc,1 FROM dbo.Joker) C ON C.PersonId=E.PersonId AND C.CurrentEmploymentId=E.EmploymentId AND C.PersonnelGroup=E.PersonnelGroup WHERE E.EmploymentId=I.EmploymentId AND E.PersonId=I.PersonId AND E.PersonnelGroup=I.PersonnelGroup AND E.IsClosed=0 AND C.ArchivedUtc IS NULL AND(E.StartDate IS NULL OR E.StartDate<=I.FromDay) AND(E.EndDate IS NULL OR E.EndDate>=I.ToDay))) THROW 51352,N'Talep güncel personel/çalışma dönemine ait olmalı.',1;
 IF EXISTS(SELECT 1 FROM inserted I WHERE I.Kind=N'OvertimePremium' AND(I.PremiumLineId IS NULL OR I.Units<>FLOOR(I.Units) OR NOT EXISTS(
 SELECT 1 FROM dbo.AccrualCalculationLines L JOIN dbo.AccrualCalculations C ON C.CalculationId=L.CalculationId JOIN dbo.AccrualPostings P ON P.CalculationId=C.CalculationId AND P.IsSealed=1 JOIN dbo.AccrualItemBalances B ON B.PostingId=P.PostingId AND B.EmploymentId=L.EmploymentId WHERE L.LineId=I.PremiumLineId AND C.ProjectId=I.ProjectId AND L.PersonId=I.PersonId AND L.EmploymentId=I.EmploymentId AND L.WorkDate=I.FromDay AND I.FromDay=I.ToDay AND I.Units<=L.PaidMinutes AND I.UnitAmount=L.HourlyAmount AND B.FinanceEntryId IS NOT NULL AND B.EffectiveAmount>0 AND NOT EXISTS(SELECT 1 FROM dbo.AccrualCorrectionTransfers T WHERE T.SourceItemId=B.ItemId)))) THROW 51353,N'Ek fazla mesai kaynağı güncel aktarılmış saatlik satır olmalı.',1;
END;
GO
CREATE TRIGGER dbo.GuardWageClaimActions ON dbo.WageClaimActions AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51354,N'Talep karar geçmişi değişmez.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN dbo.WageClaims C ON C.ClaimId=I.ClaimId WHERE C.IsReserved=0
 OR(I.Kind IN(N'Approve',N'Reject') AND(EXISTS(SELECT 1 FROM dbo.WageClaimActions A WHERE A.ClaimId=I.ClaimId AND A.ActionId<>I.ActionId AND A.Kind IN(N'Approve',N'Reject',N'Cancel')) OR EXISTS(SELECT 1 FROM dbo.WageClaimTransfers T WHERE T.ClaimId=I.ClaimId) OR dbo.SameAccountPerson(I.ActorAccountId,C.CreatedByAccountId)=1 OR(C.PersonId IS NOT NULL AND dbo.AccountRepresentsPerson(I.ActorAccountId,C.PersonId)=1)))
 OR(I.Kind=N'Cancel' AND(EXISTS(SELECT 1 FROM dbo.WageClaimTransfers T WHERE T.ClaimId=I.ClaimId) OR EXISTS(SELECT 1 FROM dbo.WageClaimActions A WHERE A.ClaimId=I.ClaimId AND A.ActionId<>I.ActionId AND A.Kind IN(N'Reject',N'Cancel'))))
 OR(I.Kind IN(N'RequestReverse',N'ApproveReverse') AND(NOT EXISTS(SELECT 1 FROM dbo.WageClaimTransfers T WHERE T.ClaimId=I.ClaimId AND T.Kind=N'Post') OR EXISTS(SELECT 1 FROM dbo.WageClaimTransfers T WHERE T.ClaimId=I.ClaimId AND T.Kind=N'Reverse')))
 OR(I.Kind=N'ApproveReverse' AND(NOT EXISTS(SELECT 1 FROM dbo.WageClaimActions A WHERE A.ClaimId=I.ClaimId AND A.Kind=N'RequestReverse') OR EXISTS(SELECT 1 FROM dbo.WageClaimActions A WHERE A.ClaimId=I.ClaimId AND A.Kind=N'RequestReverse' AND dbo.SameAccountPerson(I.ActorAccountId,A.ActorAccountId)=1) OR(C.PersonId IS NOT NULL AND dbo.AccountRepresentsPerson(I.ActorAccountId,C.PersonId)=1))))
 THROW 51355,N'Talep kararı güncel duruma veya bağımsız onay kuralına uymuyor.',1;
END;
GO
CREATE TRIGGER dbo.GuardWageFinance ON dbo.FinanceEntries AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted D LEFT JOIN inserted I ON I.EntryId=D.EntryId WHERE D.WageClaimId IS NOT NULL AND(I.EntryId IS NULL OR EXISTS(SELECT I.WageClaimId EXCEPT SELECT D.WageClaimId) OR NOT EXISTS(SELECT 1 FROM dbo.WageClaimActions A WHERE A.ClaimId=D.WageClaimId AND A.Kind=N'ApproveReverse')))
 OR EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.EntryId=I.EntryId WHERE I.WageClaimId IS NOT NULL AND D.WageClaimId IS NULL)
 THROW 51356,N'Kural belgesi yalnız bağımsız onaylı ters aktarım ile kapatılabilir.',1;
 IF EXISTS(SELECT 1 FROM inserted I LEFT JOIN deleted D ON D.EntryId=I.EntryId WHERE I.WageClaimId IS NOT NULL AND D.EntryId IS NULL AND NOT EXISTS(
 SELECT 1 FROM dbo.WageClaims C JOIN dbo.WageClaimActions A ON A.ClaimId=C.ClaimId AND A.Kind=N'Approve' WHERE C.ClaimId=I.WageClaimId AND C.IsReserved=1 AND C.ProjectId=I.ProjectId AND C.Amount=I.Amount AND C.FromDay=I.PeriodStart AND C.ToDay=I.PeriodEnd AND I.Reference=N'WAGE-'+REPLACE(CONVERT(nvarchar(36),C.ClaimId),N'-',N'') AND I.ReversedUtc IS NULL AND I.AccrualCalculationId IS NULL
 AND NOT EXISTS(SELECT 1 FROM dbo.WageClaimActions X WHERE X.ClaimId=C.ClaimId AND X.Kind IN(N'Cancel',N'Reject'))
 AND((C.Kind=N'ProjectExpense' AND I.Kind=N'Expense') OR(C.Kind<>N'ProjectExpense' AND I.Kind=N'Accrual' AND I.PersonId=C.PersonId AND I.PersonnelGroup=C.PersonnelGroup)))) THROW 51357,N'Finans belgesi onaylı kural hesabına ait değil.',1;
END;
GO
CREATE TRIGGER dbo.GuardWageLedger ON dbo.PaymentLedger AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted WHERE WageClaimId IS NOT NULL) OR EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.LedgerId=I.LedgerId WHERE I.WageClaimId IS NOT NULL) THROW 51358,N'Kural aktarım defteri değişmez.',1;
 IF EXISTS(SELECT 1 FROM inserted I WHERE I.WageClaimId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dbo.WageClaims C JOIN dbo.FinanceEntries F ON F.WageClaimId=C.ClaimId AND F.EntryId=I.FinanceEntryId WHERE C.ClaimId=I.WageClaimId AND C.Kind<>N'ProjectExpense' AND C.IsReserved=1 AND I.PersonId=C.PersonId AND I.PersonnelGroup=C.PersonnelGroup AND I.ProjectId=C.ProjectId AND I.TC=(SELECT TC FROM dbo.PersonnelIdentities WHERE PersonId=C.PersonId)
 AND((I.WageTransferKind=N'Post' AND I.Delta=C.Amount AND F.ReversedUtc IS NULL AND EXISTS(SELECT 1 FROM dbo.WageClaimActions A WHERE A.ClaimId=C.ClaimId AND A.Kind=N'Approve')) OR(I.WageTransferKind=N'Reverse' AND I.Delta=-C.Amount AND F.ReversedUtc IS NOT NULL AND EXISTS(SELECT 1 FROM dbo.WageClaimActions A WHERE A.ClaimId=C.ClaimId AND A.Kind=N'ApproveReverse'))))) THROW 51359,N'Defter kural talebi ve bağımsız onayla uzlaşmıyor.',1;
END;
GO
CREATE TRIGGER dbo.GuardWageTransfers ON dbo.WageClaimTransfers AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM deleted) THROW 51360,N'Ücret/gider aktarım makbuzu değişmez.',1;
 IF EXISTS(SELECT 1 FROM inserted I JOIN dbo.WageClaims C ON C.ClaimId=I.ClaimId JOIN dbo.FinanceEntries F ON F.EntryId=I.FinanceEntryId WHERE C.IsReserved=0 OR F.WageClaimId IS NULL OR F.WageClaimId<>C.ClaimId OR F.ProjectId<>C.ProjectId OR I.Amount<>CASE WHEN I.Kind=N'Post' THEN C.Amount ELSE -C.Amount END
 OR(I.Kind=N'Post' AND(F.ReversedUtc IS NOT NULL OR NOT EXISTS(SELECT 1 FROM dbo.WageClaimActions A WHERE A.ClaimId=C.ClaimId AND A.Kind=N'Approve')))
 OR(I.Kind=N'Reverse' AND(F.ReversedUtc IS NULL OR NOT EXISTS(SELECT 1 FROM dbo.WageClaimTransfers T WHERE T.ClaimId=C.ClaimId AND T.Kind=N'Post' AND T.FinanceEntryId=I.FinanceEntryId) OR NOT EXISTS(SELECT 1 FROM dbo.WageClaimActions A WHERE A.ClaimId=C.ClaimId AND A.Kind=N'ApproveReverse')))
 OR(C.Kind=N'ProjectExpense' AND(I.LedgerId IS NOT NULL OR I.BeforeBalance IS NOT NULL OR I.AfterBalance IS NOT NULL))
 OR(C.Kind<>N'ProjectExpense' AND(I.LedgerId IS NULL OR I.BeforeBalance IS NULL OR I.AfterBalance IS NULL OR I.AfterBalance<>I.BeforeBalance+I.Amount OR NOT EXISTS(
 SELECT 1 FROM dbo.PaymentLedger L JOIN dbo.EmploymentRecordLinks E ON E.RecordKind=N'Ledger' AND E.RecordKey=CONVERT(nvarchar(64),L.LedgerId) AND E.EmploymentId=C.EmploymentId JOIN dbo.EmploymentRecordLinks G ON G.RecordKind=N'Finance' AND G.RecordKey=CONVERT(nvarchar(64),F.EntryId) AND G.EmploymentId=C.EmploymentId
 JOIN(SELECT PersonId,CurrentEmploymentId,PARA,ArchivedUtc,0 PersonnelGroup FROM dbo.Calisan UNION ALL SELECT PersonId,CurrentEmploymentId,PARA,ArchivedUtc,1 FROM dbo.Joker) P ON P.PersonId=C.PersonId AND P.PersonnelGroup=C.PersonnelGroup AND P.CurrentEmploymentId=C.EmploymentId AND P.ArchivedUtc IS NULL AND P.PARA=I.AfterBalance WHERE L.LedgerId=I.LedgerId AND L.WageClaimId=C.ClaimId AND L.WageTransferKind=I.Kind AND L.Delta=I.Amount))))
 THROW 51361,N'Kural aktarımı belge, çalışma dönemi, bakiye veya defterle uzlaşmıyor.',1;
END;

GO
CREATE TRIGGER dbo.Audit_WagePolicies ON dbo.WagePolicies AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'WageRule.Change',N'WagePolicies',CONVERT(nvarchar(100),COALESCE(I.PolicyId,D.PolicyId)),CASE WHEN D.PolicyId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',CASE WHEN D.PolicyId IS NULL THEN NULL ELSE(SELECT D.[PolicyId],D.[ProjectId],D.[PreviousPolicyId],D.[RuleCode],D.[Revision],D.[Kind],D.[ValidFrom],D.[ValidTo],D.[UnitAmount],D.[PremiumFactor],D.[Divisor],D.[Reason] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,CASE WHEN I.PolicyId IS NULL THEN NULL ELSE(SELECT I.[PolicyId],I.[ProjectId],I.[PreviousPolicyId],I.[RuleCode],I.[Revision],I.[Kind],I.[ValidFrom],I.[ValidTo],I.[UnitAmount],I.[PremiumFactor],I.[Divisor],I.[Reason] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application' FROM inserted I FULL OUTER JOIN deleted D ON D.PolicyId=I.PolicyId;
END;

GO
CREATE TRIGGER dbo.Audit_WagePolicyDecisions ON dbo.WagePolicyDecisions AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'WageRule.Change',N'WagePolicyDecisions',CONVERT(nvarchar(100),COALESCE(I.DecisionId,D.DecisionId)),CASE WHEN D.DecisionId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',CASE WHEN D.DecisionId IS NULL THEN NULL ELSE(SELECT D.[DecisionId],D.[PolicyId],D.[Kind],D.[ActorAccountId],D.[Actor],D.[CreatedUtc],D.[Reason] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,CASE WHEN I.DecisionId IS NULL THEN NULL ELSE(SELECT I.[DecisionId],I.[PolicyId],I.[Kind],I.[ActorAccountId],I.[Actor],I.[CreatedUtc],I.[Reason] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application' FROM inserted I FULL OUTER JOIN deleted D ON D.DecisionId=I.DecisionId;
END;

GO
CREATE TRIGGER dbo.Audit_WageClaims ON dbo.WageClaims AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'WageRule.Change',N'WageClaims',CONVERT(nvarchar(100),COALESCE(I.ClaimId,D.ClaimId)),CASE WHEN D.ClaimId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',CASE WHEN D.ClaimId IS NULL THEN NULL ELSE(SELECT D.[ClaimId],D.[ProjectId],D.[PolicyId],D.[Kind],D.[PersonId],D.[EmploymentId],D.[FromDay],D.[ToDay],D.[DocumentKey],D.[PremiumLineId],D.[Units],D.[UnitAmount],D.[PremiumFactor],D.[Divisor],D.[Amount],D.[SnapshotHash],D.[IsReserved],D.[ReleasedUtc],D.[Reason] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,CASE WHEN I.ClaimId IS NULL THEN NULL ELSE(SELECT I.[ClaimId],I.[ProjectId],I.[PolicyId],I.[Kind],I.[PersonId],I.[EmploymentId],I.[FromDay],I.[ToDay],I.[DocumentKey],I.[PremiumLineId],I.[Units],I.[UnitAmount],I.[PremiumFactor],I.[Divisor],I.[Amount],I.[SnapshotHash],I.[IsReserved],I.[ReleasedUtc],I.[Reason] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application' FROM inserted I FULL OUTER JOIN deleted D ON D.ClaimId=I.ClaimId;
END;

GO
CREATE TRIGGER dbo.Audit_WageClaimActions ON dbo.WageClaimActions AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'WageRule.Change',N'WageClaimActions',CONVERT(nvarchar(100),COALESCE(I.ActionId,D.ActionId)),CASE WHEN D.ActionId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',CASE WHEN D.ActionId IS NULL THEN NULL ELSE(SELECT D.[ActionId],D.[ClaimId],D.[Kind],D.[ActorAccountId],D.[Actor],D.[CreatedUtc],D.[Reason] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,CASE WHEN I.ActionId IS NULL THEN NULL ELSE(SELECT I.[ActionId],I.[ClaimId],I.[Kind],I.[ActorAccountId],I.[Actor],I.[CreatedUtc],I.[Reason] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application' FROM inserted I FULL OUTER JOIN deleted D ON D.ActionId=I.ActionId;
END;

GO
CREATE TRIGGER dbo.Audit_WageClaimTransfers ON dbo.WageClaimTransfers AFTER INSERT,UPDATE,DELETE AS
BEGIN SET NOCOUNT ON;
 INSERT dbo.AuditLog(Actor,Action,EntityType,RecordKey,Operation,Reason,ReasonSource,OldValues,NewValues,CorrelationId,ActorAccountId,SqlPrincipal,Source)
 SELECT COALESCE(CONVERT(nvarchar(50),SESSION_CONTEXT(N'Audit.Actor')),LEFT(ORIGINAL_LOGIN(),50)),N'WageRule.Change',N'WageClaimTransfers',CONVERT(nvarchar(100),COALESCE(I.TransferId,D.TransferId)),CASE WHEN D.TransferId IS NULL THEN N'Insert' ELSE N'Update' END,
 CONVERT(nvarchar(500),SESSION_CONTEXT(N'Audit.Reason')),N'UserProvided',CASE WHEN D.TransferId IS NULL THEN NULL ELSE(SELECT D.[TransferId],D.[ClaimId],D.[Kind],D.[FinanceEntryId],D.[LedgerId],D.[Amount],D.[BeforeBalance],D.[AfterBalance],D.[ActorAccountId],D.[Actor],D.[CreatedUtc],D.[Reason] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,CASE WHEN I.TransferId IS NULL THEN NULL ELSE(SELECT I.[TransferId],I.[ClaimId],I.[Kind],I.[FinanceEntryId],I.[LedgerId],I.[Amount],I.[BeforeBalance],I.[AfterBalance],I.[ActorAccountId],I.[Actor],I.[CreatedUtc],I.[Reason] FOR JSON PATH,INCLUDE_NULL_VALUES,WITHOUT_ARRAY_WRAPPER)END,
 TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.Correlation')),TRY_CONVERT(uniqueidentifier,SESSION_CONTEXT(N'Audit.ActorId')),ORIGINAL_LOGIN(),N'Application' FROM inserted I FULL OUTER JOIN deleted D ON D.TransferId=I.TransferId;
END;
