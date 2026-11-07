-- Existing calculations remain the sole original calculation for their project/period.
ALTER TABLE dbo.AccrualCalculations ADD ParentCalculationId uniqueidentifier NULL;
GO
ALTER TABLE dbo.AccrualCalculations ADD CONSTRAINT FK_AccrualSupplementBase FOREIGN KEY(ParentCalculationId) REFERENCES dbo.AccrualCalculations(CalculationId),
 CONSTRAINT CK_AccrualSupplementSelf CHECK(ParentCalculationId IS NULL OR ParentCalculationId<>CalculationId);
ALTER TABLE dbo.AccrualCalculations DROP CONSTRAINT UX_AccrualCalculationPeriod;
GO
CREATE UNIQUE INDEX UX_AccrualCalculationPeriod ON dbo.AccrualCalculations(ProjectId,PeriodStart,PeriodEnd) WHERE ParentCalculationId IS NULL;
CREATE INDEX IX_AccrualSupplementBase ON dbo.AccrualCalculations(ParentCalculationId) WHERE ParentCalculationId IS NOT NULL;
GO
CREATE TRIGGER dbo.GuardAccrualSupplement ON dbo.AccrualCalculations AFTER INSERT,UPDATE,DELETE AS
BEGIN
 SET NOCOUNT ON;
 IF EXISTS(SELECT 1 FROM inserted I JOIN deleted D ON D.CalculationId=I.CalculationId WHERE EXISTS(SELECT I.ParentCalculationId EXCEPT SELECT D.ParentCalculationId))
  THROW 51260,N'Ek hesabın asıl hesap bağlantısı değiştirilemez.',1;
 IF EXISTS(SELECT 1 FROM inserted I WHERE I.ParentCalculationId IS NOT NULL AND NOT EXISTS(
  SELECT 1 FROM dbo.AccrualCalculations B JOIN dbo.AccrualPostings P ON P.CalculationId=B.CalculationId AND P.IsSealed=1
  WHERE B.CalculationId=I.ParentCalculationId AND B.ParentCalculationId IS NULL AND B.ProjectId=I.ProjectId AND B.PeriodStart=I.PeriodStart AND B.PeriodEnd=I.PeriodEnd AND B.IsSealed=1))
  THROW 51261,N'Ek hesap aynı proje/dönemde aktarılmış asıl hesaba bağlanmalı.',1;
END;
GO
UPDATE dbo.admin SET SessionEpoch=NEWID();
