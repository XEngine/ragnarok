$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = Split-Path -Parent $PSScriptRoot
$local = Get-Content -LiteralPath (Join-Path $projectRoot 'dev.local.json') -Raw | ConvertFrom-Json
$core = Join-Path $local.gamePath 'BepInEx/core'
[void][Reflection.Assembly]::LoadFrom((Join-Path $core 'BepInEx.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $core '0Harmony.dll'))
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $projectRoot 'src/Ragnarok/bin/Debug/net472/Ragnarok.dll'))
$migration = $assembly.GetType('Ragnarok.Configuration.ConfigMigration', $true).GetMethod('Upgrade', [Reflection.BindingFlags]'NonPublic,Static')
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('Ragnarok-config-check-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($scratch)
$path = Join-Path $scratch 'migration.cfg'
$backup = $path + '.before-modules.bak'
try {
    $legacy = @'
[Interface]
Scale = 1.2
[Inventory]
CharacterSlots = false
Style = Ragnarok
[HUD.Inventory]
Style = Vanilla
[QoL.Skills]
ExperienceGainModifierPercent = 75
[UnknownModSection]
KeepMe = custom
'@
    [IO.File]::WriteAllText($path, $legacy)
    $config = New-Object BepInEx.Configuration.ConfigFile($path, $false)
    $config.SaveOnConfigSet = $false
    [void]$migration.Invoke($null, (,$config.PSObject.BaseObject))
    [void]$migration.Invoke($null, (,$config.PSObject.BaseObject))
    $config.Save()
    $text = [IO.File]::ReadAllText($path)
    foreach ($expected in @('[HUD.Window]', 'Scale = 1.2', '[Gameplay.Equipment]', 'CharacterSlots = false', '[HUD.Inventory]', 'Style = Vanilla', '[Gameplay.Skills]', 'ExperienceGainModifierPercent = 75', '[UnknownModSection]', 'KeepMe = custom')) {
        if (-not $text.Contains($expected)) { throw "Migration lost: $expected" }
    }
    foreach ($removed in @('[Interface]', '[Inventory]', '[QoL.Skills]', 'Style = Ragnarok')) {
        if ($text.Contains($removed)) { throw "Legacy value survived: $removed" }
    }
    if ([IO.File]::ReadAllText($backup) -ne $legacy) { throw 'Migration backup was changed.' }
    Write-Host 'PASS: migration with the installed BepInEx ConfigFile, repeat migration, new-value precedence, unknown entries and backup preservation.'
} finally {
    # Only the explicit files created above are removed; no recursive deletion.
    foreach ($file in @($path, $backup)) {
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file -Force }
    }
    Remove-Item -LiteralPath $scratch
}
