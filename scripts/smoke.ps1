$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = Split-Path -Parent $PSScriptRoot
$local = Get-Content -LiteralPath (Join-Path $projectRoot 'dev.local.json') -Raw | ConvertFrom-Json
if (Get-Process valheim -ErrorAction SilentlyContinue) { throw 'Close Valheim before the smoke test.' }
& (Join-Path $projectRoot 'dev.ps1') deploy
$game = $local.gamePath
$bepConfig = Join-Path $game 'BepInEx/config/BepInEx.cfg'
$engineConfig = Join-Path $game 'BepInEx/config/com.bepis.bepinex.scriptengine.cfg'
$originalBep = [IO.File]::ReadAllText($bepConfig)
$originalEngine = [IO.File]::ReadAllText($engineConfig)
$logPath = Join-Path $game 'BepInEx/LogOutput.log'
$smokeLog = Join-Path (Split-Path -Parent $game) 'startup-smoke.log'
$process = $null
$runStarted = Get-Date
try {
    [IO.File]::WriteAllText($bepConfig, [regex]::Replace($originalBep, '(?s)(\[Logging.Console\].*?Enabled = )true', '${1}false'))
    [IO.File]::WriteAllText($engineConfig, $originalEngine.Replace('EnableFileSystemWatcher = false', 'EnableFileSystemWatcher = true'))
    $process = Start-Process -FilePath (Join-Path $game 'valheim.exe') -WorkingDirectory $game -WindowStyle Hidden -PassThru -ArgumentList @(
        '-batchmode', '-ragnarok-inventory-checks', '-savedir', ('"' + $local.savePath + '"'),
        '-logFile', ('"' + $smokeLog + '"')
    )
    $deadline = (Get-Date).AddSeconds(45)
    $reloadTriggered = $false
    $passed = $false
    do {
        Start-Sleep -Seconds 1
        $process.Refresh()
        if ((Test-Path -LiteralPath $logPath) -and (Get-Item -LiteralPath $logPath).LastWriteTime -ge $runStarted) {
            [string]$log = Get-Content -LiteralPath $logPath -Raw -Encoding UTF8
            $loadCount = [regex]::Matches($log, 'Ragnarok [0-9.]+ loaded').Count
            if ($loadCount -ge 1 -and -not $reloadTriggered) {
                Write-Host 'PASS: Plugin loaded inside Valheim.'
                # Exercise the same reload method as F6 through ScriptEngine's file watcher.
                Copy-Item -LiteralPath (Join-Path $projectRoot 'src/Ragnarok/bin/Debug/net472/Ragnarok.dll') -Destination (Join-Path $game 'BepInEx/scripts/Ragnarok.dll') -Force
                (Get-Item -LiteralPath (Join-Path $game 'BepInEx/scripts/Ragnarok.dll')).LastWriteTimeUtc = [DateTime]::UtcNow
                $reloadTriggered = $true
            }
            if ($loadCount -ge 2 -and $log.Contains('Ragnarok unloaded.')) {
                if ($log -match '\[Error') { throw 'Errors appeared during the runtime test. Inspect LogOutput.log.' }
                if ([regex]::Matches($log, 'Ragnarok inventory checks PASS').Count -lt 2) { throw 'Inventory runtime checks did not pass on both loads.' }
                if ([regex]::Matches($log, 'Ragnarok policy checks PASS').Count -lt 2) { throw 'Policy/slot/craft runtime checks did not pass on both loads.' }
                if ([regex]::Matches($log, 'Ragnarok UI lifecycle checks PASS').Count -lt 2) { throw 'Destroyed-UI lifecycle checks did not pass on both loads.' }
                Write-Host 'PASS: Old instance unloaded and new plugin instance loaded.'
                $passed = $true
                break
            }
        }
    } while (-not $process.HasExited -and (Get-Date) -lt $deadline)
    if (-not $passed) { throw "Smoke test failed or timed out. Inspect $logPath and $smokeLog." }
} finally {
    if ($process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id
        $process.WaitForExit()
    }
    [IO.File]::WriteAllText($bepConfig, $originalBep)
    [IO.File]::WriteAllText($engineConfig, $originalEngine)
}
