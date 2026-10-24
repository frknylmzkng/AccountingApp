[CmdletBinding()]
param([switch]$Full, [switch]$NoDatabase)

$ErrorActionPreference = 'Stop'
if ($Full -and $NoDatabase) { throw '-Full ve -NoDatabase birlikte kullanılamaz.' }
$taskRoot = Split-Path -Parent $PSScriptRoot
$taskStamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$taskReport = Join-Path $taskRoot ('artifacts\analysis\master-' + $taskStamp)
$taskScratch = Join-Path ([IO.Path]::GetTempPath()) ('AccountingAppTests\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $taskReport, $taskScratch | Out-Null
$taskDotnet = Join-Path $taskRoot 'dotnet.cmd'
$taskSolution = Join-Path $taskRoot 'WinFormsApp2.sln'
$taskExit = 0
$taskEngine = 'AccountingAppTestEngine_' + [Guid]::NewGuid().ToString('N')
$taskEngineCreated = $false
$taskPreviousEngine = $env:ACCOUNTINGAPP_TEST_LOCALDB_INSTANCE
$taskLocalDb = $null
$taskSummary = [ordered]@{ Started = (Get-Date).ToString('o'); Full = [bool]$Full; DatabaseFree = [bool]$NoDatabase; Scratch = $taskScratch; Build = 'NotRun'; Tests = 'NotRun'; Areas = @() }
Push-Location -LiteralPath $taskRoot
try {
    & $taskDotnet restore $taskSolution --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Kilitli bağımlılık geri yükleme başarısız.' }
    & $taskDotnet build $taskSolution -c Release --no-restore -p:TreatWarningsAsErrors=true
    if ($LASTEXITCODE -ne 0) { throw 'Derleme başarısız.' }
    $taskSummary.Build = 'Passed'
    if (-not $NoDatabase) {
        $taskSqlRoot = Join-Path $env:ProgramFiles 'Microsoft SQL Server'
        $taskLocalDb = Get-ChildItem -LiteralPath $taskSqlRoot -Directory | Where-Object { $_.Name -match '^\d+$' } | Sort-Object { [int]$_.Name } -Descending | ForEach-Object { Join-Path $_.FullName 'Tools\Binn\SqlLocalDB.exe' } | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        if (-not $taskLocalDb) { throw 'LocalDB test motoru aracı bulunamadı.' }
        & $taskLocalDb create $taskEngine
        if ($LASTEXITCODE -ne 0) { throw 'İzole test motoru oluşturulamadı.' }
        $taskEngineCreated = $true
        & $taskLocalDb start $taskEngine
        if ($LASTEXITCODE -ne 0) { throw 'İzole test motoru başlatılamadı.' }
        $env:ACCOUNTINGAPP_TEST_LOCALDB_INSTANCE = $taskEngine
        $taskSummary.TestEngine = $taskEngine
    }
    $taskFilter = 'TestCategory!=FullRestore'
    if ($Full) { $taskFilter = 'FullyQualifiedName~WinFormsApp2' }
    # FullRestore belongs to LocalDb too. A single condition also avoids cmd.exe interpreting '&' in PS 5.1.
    if ($NoDatabase) { $taskFilter = 'TestCategory!=LocalDb' }
    $taskNunitDirectory = $taskScratch.Replace('\', '/')
    & $taskDotnet test $taskSolution -c Release --no-build --no-restore --filter $taskFilter --blame-hang-timeout 45s --logger 'trx;LogFileName=master.trx' --results-directory $taskReport -- "NUnit.WorkDirectory=$taskNunitDirectory"
    if ($LASTEXITCODE -ne 0) { throw 'Testler başarısız; TRX dosyasını inceleyin.' }
    [xml]$taskTrx = Get-Content -LiteralPath (Join-Path $taskReport 'master.trx') -Raw
    $taskResults = @($taskTrx.SelectNodes("//*[local-name()='UnitTestResult']"))
    if ($taskResults.Count -eq 0) { throw 'Hiç test çalışmadı.' }
    $taskSummary.Tests = 'Passed'
    # Fifteen adapted areas. Evidence is selected from actual TRX results, not a hardcoded pass list.
    $taskAreas = [ordered]@{
        'Parola güvenliği'='Password|Legacy|Authenticate|Malformed';
        'Rol ve hesap yetkileri'='RolePolicy|AuthenticationReturns|LastAdmin|ViewerCannot';
        'Bağlantı profilleri'='DatabaseSettings|SqlExpressProfile|DataDirectory';
        'Migration ve kurulum'='StartupUpgrade|DatabaseSetupTests';
        'UnitOfWork ve rollback'='DisposeWithoutCommit|TransactionTests';
        'Personel ve arama'='AsyncCrudSearch|InputAmount';
        'İzin ve toplu plan'='BulkLeavePlan|LeaveWorkflow|LeaveInput';
        'Eşzamanlı ödeme'='ConcurrentPayments';
        'Ödeme defteri ve iptal'='ReversalRestores';
        'Transaction audit'='AuditFailure';
        'Yedek ve restore'='BackupIsVerified|RetentionOnly|RestoreOnly';
        'Saf hesaplamalar'='WeightedAverage';
        'Excel ve CSV'='ExcelPreserves|Csv';
        'Tema, kolon, taslak ve oturum'='ThemeSets|DraftIsEncrypted|SessionManagement|Preferences|GridLayout';
        'A4 ve termal rapor'='BothPaperSizes'
    }
    foreach ($taskArea in $taskAreas.GetEnumerator()) {
        $taskMatches = @($taskResults | Where-Object { $_.testName -match $taskArea.Value })
        $taskState = if ($taskMatches.Count -eq 0) { 'NotRun' } elseif (@($taskMatches | Where-Object { $_.outcome -ne 'Passed' }).Count -gt 0) { 'Failed' } else { 'Passed' }
        $taskSummary.Areas += [ordered]@{ Name=$taskArea.Key; State=$taskState; TestCount=$taskMatches.Count }
        if (-not $NoDatabase -and $taskState -ne 'Passed') { throw ('Test alanı doğrulanmadı: ' + $taskArea.Key) }
    }
} catch { $taskSummary.Error = $_.Exception.Message; $taskExit = 1 }
finally {
    $env:ACCOUNTINGAPP_TEST_LOCALDB_INSTANCE = $taskPreviousEngine
    if ($taskEngineCreated -and $taskEngine -match '^AccountingAppTestEngine_[0-9a-f]{32}$') {
        & $taskLocalDb stop $taskEngine
        & $taskLocalDb delete $taskEngine
        if ($LASTEXITCODE -ne 0) { $taskSummary.EngineCleanup = 'Failed'; $taskExit = 1 } else { $taskSummary.EngineCleanup = 'Passed' }
    }
    $taskSummary.Finished = (Get-Date).ToString('o')
    $taskEncoding = New-Object System.Text.UTF8Encoding($true)
    [IO.File]::WriteAllText((Join-Path $taskReport 'summary.json'), ($taskSummary | ConvertTo-Json -Depth 6), $taskEncoding)
    $taskLines = @('# Master test raporu', '', ('Derleme: ' + $taskSummary.Build), ('Testler: ' + $taskSummary.Tests), '', '| Alan | Durum | Test |', '|---|---|---|')
    foreach ($taskArea in $taskSummary.Areas) { $taskLines += ('| ' + $taskArea.Name + ' | ' + $taskArea.State + ' | ' + $taskArea.TestCount + ' |') }
    if ($taskSummary.Contains('Error')) { $taskLines += ('Hata: ' + $taskSummary.Error) }
    [IO.File]::WriteAllText((Join-Path $taskReport 'RAPOR.md'), ($taskLines -join [Environment]::NewLine), $taskEncoding)
    # Only delete this run's verified GUID directory under the dedicated temporary root.
    $taskAllowed = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) 'AccountingAppTests')) + [IO.Path]::DirectorySeparatorChar
    $taskResolved = [IO.Path]::GetFullPath($taskScratch)
    if ($taskResolved.StartsWith($taskAllowed, [StringComparison]::OrdinalIgnoreCase) -and (Split-Path -Leaf $taskResolved) -match '^[0-9a-f]{32}$') { Remove-Item -LiteralPath $taskResolved -Recurse -Force }
    Pop-Location
}
Write-Output ('Rapor: ' + $taskReport)
if ($taskExit -ne 0) { throw ('Otomatik test tamamlanamadı. ' + $taskSummary.Error) }
