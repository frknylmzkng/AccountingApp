-- Bind the projection explicitly so later table columns cannot shift computed amounts.
CREATE OR ALTER VIEW dbo.FinanceEffectiveEntries AS
SELECT F.EntryId,F.ProjectId,F.Kind,F.Reference,F.PersonId,F.TC,F.PersonnelGroup,
       F.PeriodStart,F.PeriodEnd,F.Amount,F.Description,F.CreatedUtc,F.CreatedBy,
       F.ReversedUtc,F.ReversedBy,F.RecordVersion,F.AccrualCalculationId,F.AccrualEmploymentId,
       COALESCE(CONVERT(decimal(18,2),X.Delta),CONVERT(decimal(18,2),0)) AS AdjustmentAmount,
       CONVERT(decimal(18,2),F.Amount+COALESCE(CONVERT(decimal(18,2),X.Delta),0)) AS EffectiveAmount,
       F.WageClaimId
FROM dbo.FinanceEntries F
OUTER APPLY (
    SELECT SUM(R.Delta) AS Delta
    FROM dbo.AccrualCorrections R
    JOIN dbo.AccrualPostingItems I ON I.ItemId=R.SourceItemId
    JOIN dbo.AccrualCorrectionTransfers T ON T.CorrectionId=R.CorrectionId
    WHERE I.FinanceEntryId=F.EntryId
) X;
