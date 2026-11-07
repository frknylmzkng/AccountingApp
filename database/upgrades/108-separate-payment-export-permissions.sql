-- Revoke old sessions before granting any access under the new policy.
-- Existing non-admin roles receive no implicit payment or export grant.
UPDATE dbo.admin SET SessionEpoch=NEWID();
ALTER TABLE dbo.admin ADD
    CanPay bit NOT NULL CONSTRAINT DF_AdminCanPay DEFAULT(0),
    CanExportPersonalData bit NOT NULL CONSTRAINT DF_AdminCanExportPersonalData DEFAULT(0);
