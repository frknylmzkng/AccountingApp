#Requires -Version 7.4
[CmdletBinding()]
param([Parameter(Mandatory)][string[]]$Roots,[Parameter(Mandatory)][string]$CertificateThumbprint,[Parameter(Mandatory)][string]$TimestampUrl)
$ErrorActionPreference='Stop'
if(-not $IsWindows -or $CertificateThumbprint -notmatch '^[0-9A-Fa-f]{40}$'){throw 'A Windows CurrentUser code-signing certificate thumbprint is required.'}
$taskUri=[uri]$TimestampUrl
if($taskUri.Scheme -ne 'https' -or $taskUri.UserInfo -or $taskUri.Fragment){throw 'HTTPS RFC3161 timestamp URL required.'}
$taskCert=Get-Item -LiteralPath ('Cert:\CurrentUser\My\'+$CertificateThumbprint)
if(-not $taskCert.HasPrivateKey -or $taskCert.NotAfter.ToUniversalTime() -lt [DateTime]::UtcNow -or @($taskCert.EnhancedKeyUsageList | Where-Object ObjectId -eq '1.3.6.1.5.5.7.3.3').Count -eq 0){throw 'Valid code-signing certificate with private key required.'}
$taskSignTool=Get-ChildItem -LiteralPath (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin') -Filter signtool.exe -Recurse | Where-Object {$_.Directory.Name -eq 'x64'} | Sort-Object FullName -Descending | Select-Object -First 1 -ExpandProperty FullName
if(-not $taskSignTool){throw 'Windows SDK x64 signtool.exe unavailable.'}
$taskSigned=@{}
foreach($taskRoot in $Roots){
 foreach($taskFile in Get-ChildItem -LiteralPath $taskRoot -File -Recurse | Where-Object Name -match '^(WinFormsApp2|AccountingApp.*)\.(exe|dll)$'){
  if($taskFile.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked product binary.'}
  $taskBefore=(Get-FileHash -LiteralPath $taskFile.FullName).Hash
  if($taskSigned.ContainsKey($taskBefore)){Copy-Item -LiteralPath $taskSigned[$taskBefore] -Destination $taskFile.FullName -Force}
  else{
   & $taskSignTool sign /sha1 $CertificateThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $taskFile.FullName
   if($LASTEXITCODE -ne 0){throw 'Code signing failed; no unsigned fallback.'}
   $taskSigned[$taskBefore]=$taskFile.FullName
  }
  $taskSignature=Get-AuthenticodeSignature -LiteralPath $taskFile.FullName
  if($taskSignature.Status -ne 'Valid' -or $taskSignature.SignerCertificate.Thumbprint -ne $CertificateThumbprint){throw 'Signed binary verification failed.'}
 }
}
