$ErrorActionPreference='Stop'
$taskRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$taskSession=[Diagnostics.Process]::GetCurrentProcess().SessionId
$taskExplorer=@(Get-Process explorer -ErrorAction SilentlyContinue | Where-Object SessionId -eq $taskSession)
$taskStarted=$false
if(-not $taskExplorer){
    $taskExplorerPath=Join-Path ([Environment]::GetFolderPath('Windows')) 'explorer.exe'
    if(-not (Test-Path -LiteralPath $taskExplorerPath)){throw 'Windows Explorer executable was not found.'}
    # This is the requested visible desktop, not a background service window.
    Start-Process -FilePath $taskExplorerPath -WindowStyle Normal | Out-Null
    $taskStarted=$true
}
$taskLog=Join-Path $taskRoot 'artifacts/analysis/startup-console-20261106'
New-Item -ItemType Directory -Path $taskLog -Force | Out-Null
@{Utc=[DateTime]::UtcNow.ToString('o');SessionId=$taskSession;ExplorerAlreadyRunning=($taskExplorer.Count -gt 0);ExplorerStarted=$taskStarted} | ConvertTo-Json -Compress | Add-Content -LiteralPath (Join-Path $taskLog 'desktop-recovery.jsonl') -Encoding UTF8
