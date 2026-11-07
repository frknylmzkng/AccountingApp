#Requires -Version 7.4
[CmdletBinding()]
param([Parameter(Mandatory)][string]$BundleDirectory,[string]$ExpectedVersion='', [switch]$RequireSigned)
$ErrorActionPreference='Stop'
$taskBundle=(Resolve-Path -LiteralPath $BundleDirectory).Path
$taskSums=Join-Path $taskBundle 'SHA256SUMS.txt'
$taskListed=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach($taskLine in Get-Content -LiteralPath $taskSums){
 if($taskLine -notmatch '^([0-9a-fA-F]{64})  ([^\\/:]+)$'){throw 'Invalid checksum entry.'}
 $taskHash=$Matches[1];$taskName=$Matches[2]
 if($taskName -in '.','..','SHA256SUMS.txt' -or -not $taskListed.Add($taskName)){throw 'Duplicate/unsafe checksum entry.'}
 $taskFile=Join-Path $taskBundle $taskName
 if((Get-Item -LiteralPath $taskFile).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked bundle file.'}
 if((Get-FileHash -LiteralPath $taskFile -Algorithm SHA256).Hash -ne $taskHash){throw ('Checksum mismatch: '+$taskName)}
}
foreach($taskFile in Get-ChildItem -LiteralPath $taskBundle -File){if($taskFile.Name -ne 'SHA256SUMS.txt' -and -not $taskListed.Contains($taskFile.Name)){throw 'Unlisted bundle file.'}}
$taskProvenance=Get-Content -LiteralPath (Join-Path $taskBundle 'provenance.json') -Raw | ConvertFrom-Json
if($taskProvenance.version -notmatch '^\d+\.\d+\.\d+(-dev)?$' -or $taskProvenance.commit -notmatch '^[0-9a-f]{40}$' -or $taskProvenance.sourceState -notin 'clean','dirty' -or $taskProvenance.signatureMode -notin 'unsigned','authenticode'){throw 'Invalid bundle identity.'}
if($ExpectedVersion -and $taskProvenance.version -ne $ExpectedVersion){throw 'Unexpected version.'}
if($RequireSigned -and $taskProvenance.signatureMode -ne 'authenticode'){throw 'Signed bundle required.'}
Add-Type -AssemblyName System.IO.Compression
$taskPackages=@();$taskBusinessHashes=@()
foreach($taskKind in @('desktop','server')){
 $taskName='AccountingApp-v'+$taskProvenance.version+'-'+$taskKind+'-win-x64.zip'
 if(-not $taskListed.Contains($taskName)){throw 'Package checksum missing.'}
 $taskArchive=[IO.Compression.ZipFile]::OpenRead((Join-Path $taskBundle $taskName))
 try{
  $taskEntries=[Collections.Generic.Dictionary[string,object]]::new([StringComparer]::OrdinalIgnoreCase);[long]$taskTotal=0
  foreach($taskEntry in $taskArchive.Entries){
   $taskPath=$taskEntry.FullName
   $taskSegments=$taskPath.TrimEnd('/').Split('/')
   if($taskPath.Contains('\') -or @($taskSegments | Where-Object {$_ -in '','.', '..' -or $_ -match '[:\x00-\x1f<>"|?*]' -or $_ -match '[ .]$' -or $_ -match '^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)'}).Count -gt 0){throw 'Unsafe ZIP path.'}
   if(($taskEntry.ExternalAttributes -band 1024) -or (($taskEntry.ExternalAttributes -shr 16) -band 61440) -eq 40960){throw 'Linked ZIP entry.'}
   if($taskPath.EndsWith('/')){continue}
   if($taskEntry.Length -gt 536870912 -or -not $taskEntries.TryAdd($taskPath,$taskEntry)){throw 'Duplicate or oversized ZIP entry.'}
   $taskTotal+=$taskEntry.Length;if($taskTotal -gt 2147483648 -or $taskEntries.Count -gt 10000){throw 'ZIP size/count limit exceeded.'}
   if($taskPath -match '(?i)(\.(bak|abk|abkey|dpapi|pfx|pem|key|mdf|ldf|draft|pending|sealing|copying|verified)$|(^|/)(server|database-profile|preferences|key-export)\.json$|\.(policy|mirror|backup)\.json$)'){throw 'Private material in package.'}
  }
  if(-not $taskEntries.ContainsKey('package-manifest.json') -or $taskEntries['package-manifest.json'].Length -gt 8388608){throw 'Manifest missing/oversized.'}
  $taskReader=[IO.StreamReader]::new($taskEntries['package-manifest.json'].Open())
  try{$taskManifestText=$taskReader.ReadToEnd()}finally{$taskReader.Dispose()}
  $taskManifest=$taskManifestText | ConvertFrom-Json
  $taskExternal=Get-Content -LiteralPath (Join-Path $taskBundle ($taskKind+'-manifest.json')) -Raw
  if($taskManifestText.TrimStart([char]0xfeff) -cne $taskExternal.TrimStart([char]0xfeff)){throw 'Internal/external manifest differs.'}
  if($taskManifest.format -ne 1 -or $taskManifest.product -ne 'AccountingApp' -or $taskManifest.kind -ne $taskKind -or $taskManifest.version -ne $taskProvenance.version -or $taskManifest.commit -ne $taskProvenance.commit -or $taskManifest.sourceState -ne $taskProvenance.sourceState -or $taskManifest.expectedSchema -ne $taskProvenance.schema -or $taskManifest.apiVersion -ne $taskProvenance.apiVersion -or $taskManifest.runtime -ne $taskProvenance.runtime -or $taskManifest.signatureMode -ne $taskProvenance.signatureMode -or $taskManifest.publisherThumbprint -ne $taskProvenance.publisherThumbprint){throw 'Package identity mismatch.'}
  $taskSeen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
  foreach($taskFile in $taskManifest.files){
   if(-not $taskSeen.Add($taskFile.path) -or $taskFile.path -eq 'package-manifest.json' -or -not $taskEntries.ContainsKey($taskFile.path) -or $taskFile.sha256 -notmatch '^[0-9A-Fa-f]{64}$'){throw 'Invalid manifest file entry.'}
   $taskEntry=$taskEntries[$taskFile.path];if($taskEntry.Length -ne $taskFile.length){throw 'Manifest file length mismatch.'}
   $taskStream=$taskEntry.Open();try{$taskHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($taskStream))}finally{$taskStream.Dispose()}
   if($taskHash -ne $taskFile.sha256){throw ('Manifest hash mismatch: '+$taskFile.path)}
  }
  if($taskSeen.Count+1 -ne $taskEntries.Count){throw 'Unlisted ZIP files.'}
  $taskPrefix=if($taskKind -eq 'desktop'){'payload/'}else{''}
  foreach($taskRequired in @('WinFormsApp2.exe','WinFormsApp2.dll','AccountingApp.BackupRecovery.exe','appsettings.json','dependency-inventory.json')){if(-not $taskSeen.Contains($taskPrefix+$taskRequired)){throw 'Required payload absent.'}}
  $taskBusinessHashes+=($taskManifest.files | Where-Object path -eq ($taskPrefix+'WinFormsApp2.dll')).sha256
  if($taskKind -eq 'desktop' -and -not $taskSeen.Contains('AccountingApp.Setup.exe')){throw 'Setup absent.'}
  if($taskKind -eq 'server' -and -not $taskSeen.Contains('AccountingApp.Server.exe')){throw 'API absent.'}
  if($RequireSigned){
   if(-not $IsWindows){throw 'Authenticode verification requires Windows.'}
   if($taskManifest.publisherThumbprint -notmatch '^[0-9A-F]{40}$'){throw 'Publisher thumbprint absent.'}
   $taskTemp=Join-Path ([IO.Path]::GetTempPath()) ('AccountingApp-signature-'+[guid]::NewGuid().ToString('N'));New-Item -ItemType Directory -Path $taskTemp | Out-Null
   try{foreach($taskFile in $taskManifest.files | Where-Object path -match '(^|/)(WinFormsApp2|AccountingApp[^/]*)\.(exe|dll)$'){
     $taskTarget=Join-Path $taskTemp ([guid]::NewGuid().ToString('N')+[IO.Path]::GetExtension($taskFile.path));$taskInput=$taskEntries[$taskFile.path].Open();$taskOutput=[IO.File]::Create($taskTarget)
     try{$taskInput.CopyTo($taskOutput)}finally{$taskInput.Dispose();$taskOutput.Dispose()}
     $taskSignature=Get-AuthenticodeSignature -LiteralPath $taskTarget
     if($taskSignature.Status -ne 'Valid' -or $taskSignature.SignerCertificate.Thumbprint -ne $taskManifest.publisherThumbprint){throw 'Authenticode verification failed.'}
    }}finally{Get-ChildItem -LiteralPath $taskTemp -File | ForEach-Object {Remove-Item -LiteralPath $_.FullName};Remove-Item -LiteralPath $taskTemp}
  }
  $taskPackages+=@{kind=$taskKind;files=$taskSeen.Count;sha256=(Get-FileHash -LiteralPath (Join-Path $taskBundle $taskName)).Hash}
 }finally{$taskArchive.Dispose()}
}
if($taskBusinessHashes.Count -ne 2 -or $taskBusinessHashes[0] -ne $taskBusinessHashes[1]){throw 'Desktop/API business assemblies differ.'}
[pscustomobject]@{version=$taskProvenance.version;schema=$taskProvenance.schema;apiVersion=$taskProvenance.apiVersion;signatureMode=$taskProvenance.signatureMode;testsRun=$false;packages=$taskPackages}
