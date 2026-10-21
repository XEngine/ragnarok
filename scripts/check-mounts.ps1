param([string]$Directory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
$local = Get-Content -LiteralPath (Join-Path $root 'dev.local.json') -Raw | ConvertFrom-Json
if (Get-Process valheim -ErrorAction SilentlyContinue) { throw 'Save and close the dev client before isolated mount checks.' }
if (-not $Directory) {
    $Directory = Join-Path (Split-Path $local.gamePath -Parent) ('mount-checks/' + (Get-Date -Format yyyyMMdd-HHmmss))
}
$Directory = [IO.Path]::GetFullPath($Directory)
New-Item -ItemType Directory -Path $Directory -Force | Out-Null
& (Join-Path $root 'dev.ps1') deploy
$result = Join-Path $Directory 'result.txt'
[IO.File]::WriteAllText($result, 'RUNNING')
$process = $null
try {
    $process = Start-Process -FilePath (Join-Path $local.gamePath 'valheim.exe') -WorkingDirectory $local.gamePath -WindowStyle Hidden -PassThru -ArgumentList @(
        '-batchmode', '-ragnarok-mount-checks', ('"' + $Directory + '"'), '-logFile', ('"' + (Join-Path $Directory 'player.log') + '"'))
    Write-Host "Isolated mount checks: $Directory"
    if (-not $process.WaitForExit(240000)) { throw "Mount checks timed out; inspect $Directory/player.log" }
    if ([IO.File]::ReadAllText($result) -ne 'PASS') { throw "Mount checks failed; inspect $Directory/player.log" }
    Write-Host 'PASS: loaded movement and unloaded-sector return of the same mount, with persistent saddle/name/health and no duplicate.'
} finally {
    if ($process -and -not $process.HasExited) { Stop-Process -Id $process.Id; $process.WaitForExit() }
}
