#Requires -Version 7.4
[CmdletBinding()]
param([switch]$Release, [string]$ReleaseTag = '', [string]$OutputDirectory = '',[string]$SigningCertificateThumbprint='',[string]$TimestampUrl='')

$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location -LiteralPath $taskRoot
try {
    [xml]$taskProps = Get-Content -LiteralPath (Join-Path $taskRoot 'Directory.Build.props') -Raw
    $taskPrefix = [string]$taskProps.Project.PropertyGroup.VersionPrefix
    $taskSchema = [int]$taskProps.Project.PropertyGroup.AccountingSchemaVersion
    $taskRuntime = [string]$taskProps.Project.PropertyGroup.AccountingRuntimeVersion
    $taskContractText = Get-Content -LiteralPath (Join-Path $taskRoot 'api/AccountingApp.Contracts/ApiContracts.cs') -Raw
    if ($taskContractText -notmatch 'const string Version = "([0-9]+\.[0-9]+)"') { throw 'API contract version could not be read.' }
    $taskApiVersion = $Matches[1]

    if ($taskPrefix -notmatch '^\d+\.\d+\.\d+$') { throw 'VersionPrefix must be a three-part numeric version.' }
    $taskLatestSchema = Get-ChildItem -LiteralPath (Join-Path $taskRoot 'database\upgrades') -Filter '*.sql' | ForEach-Object { if ($_.Name -match '^(\d+)-') { [int]$Matches[1] } } | Measure-Object -Maximum
    if ($taskSchema -ne $taskLatestSchema.Maximum) { throw 'Declared schema and upgrade files disagree.' }
    $taskCommit = (& git rev-parse HEAD | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $taskCommit -notmatch '^[0-9a-f]{40}$') { throw 'A Git commit reference is required.' }
    $taskStatus = @(& git status --porcelain --untracked-files=normal)
    if ($LASTEXITCODE -ne 0) { throw 'Source status could not be read.' }
    $taskState = if ($taskStatus.Count -eq 0) { 'clean' } else { 'dirty' }
    if ($Release) {
        if ($ReleaseTag -ne ('v' + $taskPrefix) -or $taskState -ne 'clean') { throw 'A release requires an exact version tag and a clean source tree.' }
        $taskTaggedCommit = (& git rev-parse ($ReleaseTag + '^{commit}') | Out-String).Trim()
        if ($LASTEXITCODE -ne 0 -or $taskTaggedCommit -ne $taskCommit) { throw 'Release tag does not point at the current commit.' }
    }
    elseif ($ReleaseTag) { throw 'ReleaseTag requires -Release.' }
    $taskVersion = if ($Release) { $taskPrefix } else { $taskPrefix + '-dev' }
    if([bool]$SigningCertificateThumbprint -ne [bool]$TimestampUrl){throw 'Signing requires both certificate thumbprint and timestamp URL.'}
    $taskSignatureMode=if($SigningCertificateThumbprint){'authenticode'}else{'unsigned'}
    if (-not $OutputDirectory) { $OutputDirectory = Join-Path $taskRoot ('artifacts\release-bundles\' + $taskVersion + '-' + (Get-Date).ToString('yyyyMMddHHmmssfff')) }
    $taskOutput = [IO.Path]::GetFullPath($OutputDirectory, $taskRoot)
    $taskArtifacts = [IO.Path]::GetFullPath((Join-Path $taskRoot 'artifacts')).TrimEnd('\') + '\'
    if (-not $taskOutput.StartsWith($taskArtifacts, [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $taskOutput)) { throw 'Output must be a new directory under this workspace artifacts folder.' }
    New-Item -ItemType Directory -Path $taskOutput | Out-Null
    $taskDesktop = Join-Path $taskOutput 'staging\desktop'
    $taskServer = Join-Path $taskOutput 'staging\server'
    $taskDotnet = Join-Path $taskRoot 'dotnet.cmd'
    $taskProperties = @('-c','Release','-r','win-x64','--self-contained','true',('-p:RuntimeFrameworkVersion=' + $taskRuntime),'-p:PublishTrimmed=false','-p:RestoreLockedMode=true','-p:TreatWarningsAsErrors=true',('-p:Version=' + $taskVersion),('-p:SourceRevisionId=' + $taskCommit),('-p:SourceTreeState=' + $taskState))
    if ($Release) { $taskProperties += @('-p:ReleaseBuild=true',('-p:ReleaseTag=' + $ReleaseTag)) }
    foreach ($taskPublish in @(
        @{Project='WinFormsApp2.csproj';Path=(Join-Path $taskDesktop 'payload')},
        @{Project='tools\DatabaseSetup\AccountingApp.DatabaseSetup.csproj';Path=(Join-Path $taskDesktop 'payload')},
        @{Project='tools\BackupRecovery\AccountingApp.BackupRecovery.csproj';Path=(Join-Path $taskDesktop 'payload')},
        @{Project='tools\WindowsSetup\AccountingApp.Setup.csproj';Path=$taskDesktop},
        @{Project='api\AccountingApp.Server\AccountingApp.Server.csproj';Path=$taskServer},
        @{Project='tools\BackupRecovery\AccountingApp.BackupRecovery.csproj';Path=$taskServer}
    )) {
        & $taskDotnet publish $taskPublish.Project @taskProperties -o $taskPublish.Path
        if ($LASTEXITCODE -ne 0) { throw ('Publish failed: ' + $taskPublish.Project) }
    }
    if($SigningCertificateThumbprint){& (Join-Path $PSScriptRoot 'Sign-ProductBinaries.ps1') -Roots @($taskDesktop,$taskServer) -CertificateThumbprint $SigningCertificateThumbprint -TimestampUrl $TimestampUrl}
    foreach($taskInventoryRoot in @((Join-Path $taskDesktop 'payload'),$taskServer)){
        $taskDependencies=@{}
        foreach($taskDeps in Get-ChildItem -LiteralPath $taskInventoryRoot -Filter '*.deps.json' -File){
            $taskLibraries=(Get-Content -LiteralPath $taskDeps.FullName -Raw | ConvertFrom-Json).libraries
            foreach($taskLibrary in $taskLibraries.PSObject.Properties){if($taskLibrary.Value.type -eq 'package'){$taskDependencies[$taskLibrary.Name]=$true}}
        }
        $taskInventory=[ordered]@{format=1;product='AccountingApp';version=$taskVersion;runtime=$taskRuntime;dependencies=@($taskDependencies.Keys | Sort-Object | ForEach-Object {$taskSplit=$_.LastIndexOf('/');@{name=$_.Substring(0,$taskSplit);version=$_.Substring($taskSplit+1)}});licenseAuditCompleted=$false;vulnerabilityScanCompleted=$false}
        $taskInventory | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $taskInventoryRoot 'dependency-inventory.json') -Encoding UTF8
    }
    function Confirm-Binary([string]$Path) {
        $taskInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($Path)
        if ($taskInfo.FileVersion -ne ($taskPrefix + '.0') -or $taskInfo.ProductVersion -ne ($taskVersion + '+' + $taskCommit)) { throw ('Binary version mismatch: ' + [IO.Path]::GetFileName($Path)) }
    }
    foreach ($taskBinary in @('WinFormsApp2.exe','WinFormsApp2.dll','AccountingApp.DatabaseSetup.exe','AccountingApp.BackupRecovery.exe')) { Confirm-Binary (Join-Path $taskDesktop ('payload\' + $taskBinary)) }
    Confirm-Binary (Join-Path $taskDesktop 'AccountingApp.Setup.exe')
    Confirm-Binary (Join-Path $taskServer 'AccountingApp.Server.exe')
    Confirm-Binary (Join-Path $taskServer 'AccountingApp.BackupRecovery.exe')
    if ((Get-FileHash -LiteralPath (Join-Path $taskDesktop 'payload\WinFormsApp2.dll')).Hash -ne (Get-FileHash -LiteralPath (Join-Path $taskServer 'WinFormsApp2.dll')).Hash) { throw 'Desktop/server business assemblies differ.' }
    $taskDefaultProfile = Get-Content -LiteralPath (Join-Path $taskDesktop 'payload\appsettings.json') -Raw | ConvertFrom-Json
    if ($taskDefaultProfile.Database.Server -ne '(LocalDB)\MSSQLLocalDB' -or $taskDefaultProfile.Database.FilePath -ne '%USERPROFILE%\Documents\Muhasebe2.mdf' -or $null -ne $taskDefaultProfile.Database.Name) { throw 'The distributable desktop profile must be the generic template, not a private database profile.' }
    foreach ($taskPackage in @(@{Kind='desktop';Path=$taskDesktop},@{Kind='server';Path=$taskServer})) {
        $taskFiles = @(Get-ChildItem -LiteralPath $taskPackage.Path -File -Recurse | Sort-Object FullName | ForEach-Object {
            if ($_.Name -in 'server.json','database-profile.json','preferences.json','keys.dpapi','key-export.json' -or $_.Name -match '\.(policy|mirror|backup)\.json$' -or $_.Extension -in '.pfx','.pem','.key','.mdf','.ldf','.bak','.abk','.abkey','.dpapi','.sealing','.copying','.pending','.draft','.verified') { throw ('Private file found in publish output: ' + $_.Name) }
            [ordered]@{path=[IO.Path]::GetRelativePath($taskPackage.Path,$_.FullName).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash;length=$_.Length}
        })
        $taskManifest = [ordered]@{format=1;product='AccountingApp';kind=$taskPackage.Kind;version=$taskVersion;fileVersion=($taskPrefix+'.0');commit=$taskCommit;sourceState=$taskState;expectedSchema=$taskSchema;apiVersion=$taskApiVersion;runtime=$taskRuntime;signatureMode=$taskSignatureMode;publisherThumbprint=$SigningCertificateThumbprint.ToUpperInvariant();files=$taskFiles}
        $taskManifestPath = Join-Path $taskPackage.Path 'package-manifest.json'
        $taskManifest | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $taskManifestPath -Encoding UTF8
        Copy-Item -LiteralPath $taskManifestPath -Destination (Join-Path $taskOutput ($taskPackage.Kind + '-manifest.json'))
        $taskZip = Join-Path $taskOutput ('AccountingApp-v' + $taskVersion + '-' + $taskPackage.Kind + '-win-x64.zip')
        Compress-Archive -Path (Join-Path $taskPackage.Path '*') -DestinationPath $taskZip -CompressionLevel Optimal
    }
    $taskProvenance = [ordered]@{format=1;version=$taskVersion;tag=$ReleaseTag;commit=$taskCommit;sourceState=$taskState;builtUtc=[DateTime]::UtcNow.ToString('o');runtime=$taskRuntime;schema=$taskSchema;apiVersion=$taskApiVersion;signatureMode=$taskSignatureMode;publisherThumbprint=$SigningCertificateThumbprint.ToUpperInvariant();testsRunByPackaging=$false;releaseBuild=[bool]$Release}
    $taskProvenance | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskOutput 'provenance.json') -Encoding UTF8
    @("AccountingApp $taskVersion",'',"Commit reference: $taskCommit",("Source state: " + $taskState),"Schema: $taskSchema",'', 'Desktop ZIP: extract the entire package and open AccountingApp.Setup.exe. The setup wizard preserves previous versions and supports existing connections, HTTPS API and a new LocalDB.', 'Server ZIP: configure the server profile and HTTPS certificate separately; private configuration is not bundled.', 'Existing databases are not silently recreated. Resolve pending payments and close the desktop app before installing.', 'Packaging does not run tests or apply live database upgrades. See CHANGELOG.md and docs/SURUMLU_DAGITIM_VE_KURULUM.md.') | Set-Content -LiteralPath (Join-Path $taskOutput 'release-notes.md') -Encoding UTF8
    foreach($taskScript in @('Verify-WindowsBundle.ps1','Get-DeploymentPlan.ps1')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot $taskScript) -Destination (Join-Path $taskOutput $taskScript)}
    $taskChecksums = @(Get-ChildItem -LiteralPath $taskOutput -File | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object Name | ForEach-Object { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name })
    $taskChecksums | Set-Content -LiteralPath (Join-Path $taskOutput 'SHA256SUMS.txt') -Encoding ASCII
    $taskVerification=& (Join-Path $PSScriptRoot 'Verify-WindowsBundle.ps1') -BundleDirectory $taskOutput -ExpectedVersion $taskVersion -RequireSigned:([bool]$SigningCertificateThumbprint)
    $taskVerification | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskOutput 'verification.json') -Encoding UTF8
    $taskChecksums = @(Get-ChildItem -LiteralPath $taskOutput -File | Where-Object Name -ne 'SHA256SUMS.txt' | Sort-Object Name | ForEach-Object { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name })
    $taskChecksums | Set-Content -LiteralPath (Join-Path $taskOutput 'SHA256SUMS.txt') -Encoding ASCII
    if ($env:GITHUB_OUTPUT) { 'bundle=' + $taskOutput | Add-Content -LiteralPath $env:GITHUB_OUTPUT -Encoding UTF8 }
    Write-Output ('Release bundle prepared: ' + $taskOutput)
}
finally { Pop-Location }
