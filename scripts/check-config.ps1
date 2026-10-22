$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = Split-Path -Parent $PSScriptRoot
$local = Get-Content -LiteralPath (Join-Path $projectRoot 'dev.local.json') -Raw | ConvertFrom-Json
$core = Join-Path $local.gamePath 'BepInEx/core'
[void][Reflection.Assembly]::LoadFrom((Join-Path $core 'BepInEx.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $core '0Harmony.dll'))
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $projectRoot 'src/Ironbound/bin/Debug/net472/Ironbound.dll'))
$migration = $assembly.GetType('Ironbound.Configuration.ConfigMigration', $true).GetMethod('Upgrade', [Reflection.BindingFlags]'NonPublic,Static')
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('Ironbound-config-check-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($scratch)
$path = Join-Path $scratch 'migration.cfg'
$backup = $path + '.before-modules.bak'
$brandLegacy = Join-Path $scratch 'com.kernelpanic.ragnarok.cfg'
$brandCurrent = Join-Path $scratch 'com.kernelpanic.ironbound.cfg'
try {
    $legacy = @'
[Interface]
Scale = 1.2
[Inventory]
CharacterSlots = false
Style = Ironbound
[HUD.Inventory]
Style = Vanilla
[QoL.Skills]
ExperienceGainModifierPercent = 75
[QualityOfLife.NearbyChests]
Enabled = false
Range = 80
PersonalRange = 60
Crafting = false
Building = false
Stations = false
[QualityOfLife.ChestSearch]
Enabled = true
Range = 20
PersonalRange = 10
Shortcut = F + LeftControl
[QualityOfLife.QuickStack]
Enabled = true
KeepTools = false
[HUD.Hotbar]
SecondRow = true
SwitchRowKey = LeftAlt
Scale = 1.2
[QualityOfLife.Hotbar]
SecondRow = true
SwitchRowKey = LeftControl
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
    foreach ($expected in @('[HUD.Window]', 'Scale = 1.2', '[Gameplay.Equipment]', 'CharacterSlots = false', '[HUD.Inventory]', 'Style = Vanilla', '[Gameplay.Skills]', 'ExperienceGainModifierPercent = 75', '[UnknownModSection]', 'KeepMe = custom', '[QualityOfLife.NearbyChests]', 'Enabled = false', 'Range = 80', 'PersonalRange = 60', 'Shortcut = F + LeftControl', '[HUD.Hotbar]')) {
        if (-not $text.Contains($expected)) { throw "Migration lost: $expected" }
    }
    foreach ($removed in @('[Interface]', '[Inventory]', '[QoL.Skills]', 'Style = Ironbound', '[QualityOfLife.ChestSearch]', '[QualityOfLife.QuickStack]', 'Crafting =', 'Building =', 'Stations =', 'Range = 20', 'PersonalRange = 10', 'KeepTools =', 'KeepWeapons =', 'KeepConsumables =', 'SecondRow =', 'SwitchRowKey =', '[QualityOfLife.Hotbar]')) {
        if ($text.Contains($removed)) { throw "Legacy value survived: $removed" }
    }
    if ([IO.File]::ReadAllText($backup) -ne $legacy) { throw 'Migration backup was changed.' }
    Write-Host 'PASS: migration with the installed BepInEx ConfigFile, repeat migration, new-value precedence, unknown entries and backup preservation.'
    foreach ($name in @('UnityEngine.CoreModule.dll', 'UnityEngine.InputLegacyModule.dll', 'UnityEngine.dll')) {
        [void][Reflection.Assembly]::LoadFrom((Join-Path $local.gamePath "valheim_Data/Managed/$name"))
    }
    $storageType = $assembly.GetType('Ironbound.Features.NearbyStorage.StorageSettings', $true)
    $flags = [Reflection.BindingFlags]'NonPublic,Instance'
    $storage = $storageType.GetConstructors($flags)[0].Invoke((,$config.PSObject.BaseObject))
    $enabled = $storageType.GetField('Enabled', $flags).GetValue($storage)
    $range = $storageType.GetField('Range', $flags).GetValue($storage)
    $personal = $storageType.GetField('PersonalRange', $flags).GetValue($storage)
    $effective = $storageType.GetProperty('EffectiveRange', $flags)
    if ($enabled.Value -ne $false -or $effective.GetValue($storage) -ne 60) { throw 'Unified preferences were not bound.' }
    $range.Value = 25
    if ($effective.GetValue($storage) -ne 25 -or $personal.Value -ne 60) { throw 'Shared server cap did not preserve preference.' }
    $personal.Value = 10
    if ($effective.GetValue($storage) -ne 10) { throw 'Personal range cannot reduce shared cap.' }
    foreach ($removed in @('Crafting', 'Building', 'Stations', 'ChestSearchRange', 'ChestSearch', 'QuickStack', 'KeepWeapons', 'KeepConsumables', 'KeepTools')) {
        if ($null -ne $storageType.GetField($removed, $flags)) { throw "Separate chest policy remains: $removed" }
    }
    Write-Host 'PASS: combined chest settings bind preserved values and apply one shared personal/server radius.'
    $legacyBrandText = "[HUD]`nStyle = Ragnarok`n[HUD.Window]`nScale = 1.25`n"
    [IO.File]::WriteAllText($brandLegacy, $legacyBrandText)
    $imported = New-Object BepInEx.Configuration.ConfigFile($brandCurrent, $false)
    $imported.SaveOnConfigSet = $false
    [void]$migration.Invoke($null, (,$imported.PSObject.BaseObject))
    $bind = [BepInEx.Configuration.ConfigFile].GetMethods() | Where-Object {
        $_.Name -eq 'Bind' -and $_.IsGenericMethodDefinition -and $_.GetParameters().Count -eq 4 -and
        $_.GetParameters()[0].ParameterType -eq [string] -and $_.GetParameters()[3].ParameterType -eq [string]
    } | Select-Object -First 1
    $style = $bind.MakeGenericMethod([string]).Invoke($imported, @('HUD','Style','Vanilla',''))
    if ($style.Value -ne 'Ironbound' -or [IO.File]::ReadAllText($brandLegacy) -ne $legacyBrandText) { throw 'Brand migration lost style or changed the source config.' }
    $style.Value = 'Vanilla'; $imported.Save()
    [void]$migration.Invoke($null, (,$imported.PSObject.BaseObject))
    if ($style.Value -ne 'Vanilla') { throw 'Brand migration overwrote current preferences.' }
    Write-Host 'PASS: installed BepInEx imports old GUID config before binding, converts old style and preserves both source and newer preferences.'
} finally {
    # Only the explicit files created above are removed; no recursive deletion.
    foreach ($file in @($path, $backup, $brandLegacy, $brandCurrent, ($brandCurrent + '.before-modules.bak'))) {
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file -Force }
    }
    Remove-Item -LiteralPath $scratch
}
