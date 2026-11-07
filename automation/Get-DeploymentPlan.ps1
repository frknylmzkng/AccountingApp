#Requires -Version 7.4
[CmdletBinding()]
param([Parameter(Mandatory)][string]$BundleDirectory,[string]$ActiveInstallationPath='')
$ErrorActionPreference='Stop'
$taskBundle=(Resolve-Path -LiteralPath $BundleDirectory).Path
$taskVerified=& (Join-Path $PSScriptRoot 'Verify-WindowsBundle.ps1') -BundleDirectory $taskBundle
$taskNew=Get-Content -LiteralPath (Join-Path $taskBundle 'server-manifest.json') -Raw | ConvertFrom-Json
$taskOld=$null;$taskMigrationMatch=$false
if($ActiveInstallationPath){
 $taskOld=Get-Content -LiteralPath $ActiveInstallationPath -Raw | ConvertFrom-Json
 $taskOldManifest=Join-Path $taskOld.ServerRoot 'package-manifest.json'
 if(Test-Path -LiteralPath $taskOldManifest){
  $taskOldPackage=Get-Content -LiteralPath $taskOldManifest -Raw | ConvertFrom-Json
  $taskOldSql=@($taskOldPackage.files | Where-Object path -like 'database/upgrades/*.sql' | Sort-Object path | ForEach-Object {$_.path+':'+$_.sha256})
  $taskNewSql=@($taskNew.files | Where-Object path -like 'database/upgrades/*.sql' | Sort-Object path | ForEach-Object {$_.path+':'+$_.sha256})
  $taskMigrationMatch=$taskOldSql.Count -gt 0 -and ($taskOldSql -join "`n") -ceq ($taskNewSql -join "`n")
 }
}
[pscustomobject]@{
 format=1;targetVersion=$taskNew.version;targetSchema=$taskNew.expectedSchema;targetApi=$taskNew.apiVersion;signatureMode=$taskNew.signatureMode
 currentVersion=if($taskOld){$taskOld.Version}else{'unknown'};currentSchema=if($taskOld){$taskOld.Schema}else{$null};currentApi=if($taskOld){$taskOld.ApiVersion}else{'unknown'}
 backupRequired=$true;closeClientsRequired=$true;resolvePendingPaymentsRequired=$true
 schemaMigrationReviewRequired= -not ($taskOld -and $taskOld.Schema -eq $taskNew.expectedSchema -and $taskMigrationMatch)
 apiCoordinationRequired= -not ($taskOld -and $taskOld.ApiVersion -eq $taskNew.apiVersion)
 codeOnlyRollbackCandidate=[bool]($taskOld -and $taskOld.Schema -eq $taskNew.expectedSchema -and $taskMigrationMatch)
 rollbackStillRequiresDatabaseAndPendingPaymentReview=$true
 steps=@('Verify source/checksums/publisher','Record and verify independent recovery key and backup','Resolve pending operations and close all clients','Stop API and review schema migration separately','Extract to new version directory; preserve private profiles and Windows account','Start API; verify TLS/version/schema and scope','Update all clients and shortcuts','Keep previous version; record actual paths and hashes')
 databaseContacted=$false;processesChanged=$false;filesInstalled=$false;testsRun=$false
}
