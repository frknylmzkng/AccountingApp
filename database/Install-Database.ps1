[CmdletBinding()]
param(
    [string]$ConfigurationPath = (Join-Path $PSScriptRoot '..\appsettings.json'),
    [string]$SetupExecutable = (Join-Path $PSScriptRoot '..\AccountingApp.DatabaseSetup.exe')
)

$ErrorActionPreference = 'Stop'
$taskConfiguration = (Resolve-Path -LiteralPath $ConfigurationPath).Path
$taskExecutable = (Resolve-Path -LiteralPath $SetupExecutable).Path

# Passwords are requested by the console application, never passed on the command line.
# A quote cannot occur in a Windows file name; quote the full path to preserve spaces.
$taskArguments = '--config "' + $taskConfiguration + '"'
$taskProcess = Start-Process -FilePath $taskExecutable -ArgumentList $taskArguments -NoNewWindow -Wait -PassThru
if ($taskProcess.ExitCode -ne 0) {
    throw "Veritabanı kurulumu tamamlanamadı (çıkış kodu: $($taskProcess.ExitCode))."
}
