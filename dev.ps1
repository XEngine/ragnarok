param(
    [Parameter(Position = 0)]
    [ValidateSet('build', 'deploy', 'play', 'logs', 'doctor', 'package', 'watch', 'test')]
    [string]$Action = 'build',
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = $PSScriptRoot
$configFile = Join-Path $projectRoot 'dev.local.json'
if (-not (Test-Path -LiteralPath $configFile)) {
    throw 'Run scripts/setup.ps1 first, or copy dev.example.json to dev.local.json and edit its paths.'
}
$local = Get-Content -LiteralPath $configFile -Raw | ConvertFrom-Json
$gamePath = [IO.Path]::GetFullPath($local.gamePath)
$sdk = $local.dotnetPath
$project = Join-Path $projectRoot 'src/Ragnarok/Ragnarok.csproj'
$output = Join-Path $projectRoot "src/Ragnarok/bin/$Configuration/net472"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_USE_POLLING_FILE_WATCHER = '1'

function Build-Mod {
    Push-Location $projectRoot
    try {
        & $sdk build $project --configuration $Configuration "-p:GamePath=$gamePath" --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Build failed; nothing was deployed.' }
    } finally { Pop-Location }
}

function Deploy-Mod {
    Build-Mod
    $normalPlugin = Join-Path $gamePath 'BepInEx/plugins/Ragnarok/Ragnarok.dll'
    if (Test-Path -LiteralPath $normalPlugin) {
        throw 'Ragnarok is also installed in plugins. Remove that copy before using ScriptEngine.'
    }
    $destination = Join-Path $gamePath 'BepInEx/scripts'
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    # Copy symbols before the DLL. Auto-reload is disabled; press F6 after deployment.
    Copy-Item -LiteralPath (Join-Path $output 'Ragnarok.pdb') -Destination $destination -Force
    Copy-Item -LiteralPath (Join-Path $output 'Ragnarok.dll') -Destination $destination -Force
    Write-Host 'Deployed. Press F6 in Valheim to reload Ragnarok.'
}

switch ($Action) {
    'test' {
        Push-Location $projectRoot
        try {
            & $sdk run --project (Join-Path $projectRoot 'tests/Ragnarok.Tests/Ragnarok.Tests.csproj')
            if ($LASTEXITCODE -ne 0) { throw 'Regression tests failed.' }
        } finally { Pop-Location }
    }
    'build' { Build-Mod }
    'deploy' { Deploy-Mod }
    'play' {
        $running = Get-Process valheim -ErrorAction SilentlyContinue
        if ($running) { throw 'Valheim is already running. Use deploy and F6, or close it before launching.' }
        Deploy-Mod
        New-Item -ItemType Directory -Path $local.savePath -Force | Out-Null
        # The user explicitly requested this interactive game launch.
        Start-Process -FilePath (Join-Path $gamePath 'valheim.exe') -WorkingDirectory $gamePath -ArgumentList @(
            '-console', '-savedir', ('"' + $local.savePath + '"')
        )
    }
    'logs' {
        $log = Join-Path $gamePath 'BepInEx/LogOutput.log'
        if (-not (Test-Path -LiteralPath $log)) { throw 'No game log yet. Launch the development game first.' }
        Get-Content -LiteralPath $log -Tail 60 -Wait
    }
    'doctor' {
        foreach ($path in @(
            $sdk,
            (Join-Path $gamePath 'valheim.exe'),
            (Join-Path $gamePath 'valheim_Data/Managed/assembly_valheim.dll'),
            (Join-Path $gamePath 'BepInEx/core/BepInEx.dll'),
            (Join-Path $gamePath 'BepInEx/plugins/ScriptEngine.dll')
        )) {
            if (-not (Test-Path -LiteralPath $path)) { throw "Missing: $path" }
            Write-Host "OK: $path"
        }
        & $sdk --version
        if ($LASTEXITCODE -ne 0) { throw 'SDK check failed.' }
        Write-Host "Separate test saves: $($local.savePath)"
    }
    'package' {
        $Configuration = 'Release'
        $output = Join-Path $projectRoot 'src/Ragnarok/bin/Release/net472'
        Build-Mod
        $artifacts = Join-Path $projectRoot 'artifacts'
        $staging = Join-Path $artifacts ('package-' + [Guid]::NewGuid().ToString('N'))
        $pluginDir = Join-Path $staging 'BepInEx/plugins/Ragnarok'
        New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $output 'Ragnarok.dll') -Destination $pluginDir
        Copy-Item -LiteralPath (Join-Path $projectRoot 'docs/INSTALL.md') -Destination $staging
        $zip = Join-Path $artifacts 'Ragnarok-local.zip'
        Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip -Force
        Write-Host "Created $zip (our plugin only; install BepInEx separately)."
    }
    'watch' {
        function Get-SourceFingerprint {
            $sources = @(
                Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Recurse -File |
                    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and $_.Extension -in @('.cs', '.csproj') }
                Get-ChildItem -LiteralPath $projectRoot -File |
                    Where-Object { $_.Extension -in @('.props', '.targets') -or $_.Name -eq 'global.json' }
            )
            return (($sources | Sort-Object FullName | ForEach-Object {
                $_.FullName + ':' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
            }) -join '|')
        }
        $previous = Get-SourceFingerprint
        try { Deploy-Mod } catch { Write-Warning $_.Exception.Message }
        Write-Host 'Watching source files. Save code to rebuild/deploy; press F6 in game. Ctrl+C stops.'
        while ($true) {
            Start-Sleep -Seconds 1
            $current = Get-SourceFingerprint
            if ($current -ne $previous) {
                Start-Sleep -Milliseconds 500
                $previous = Get-SourceFingerprint
                try { Deploy-Mod } catch { Write-Warning $_.Exception.Message }
            }
        }
    }
}
