param(
    [string]$SourceGame = 'D:\SteamLibrary\steamapps\common\Valheim',
    [string]$DevRoot = 'D:\RagnarokDev'
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = Split-Path -Parent $PSScriptRoot
$SourceGame = [IO.Path]::GetFullPath($SourceGame).TrimEnd('\')
$DevRoot = [IO.Path]::GetFullPath($DevRoot).TrimEnd('\')
$game = Join-Path $DevRoot 'Valheim'
if (-not (Test-Path -LiteralPath (Join-Path $SourceGame 'valheim.exe'))) {
    throw "Valheim not found: $SourceGame"
}
if ($game -eq $SourceGame -or $DevRoot.StartsWith($SourceGame + '\', [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Choose a development folder outside your original game installation.'
}
if (Get-Process valheim -ErrorAction SilentlyContinue) {
    throw 'Close Valheim before setting up or refreshing the development copy.'
}
$downloads = Join-Path $DevRoot 'downloads'
$sdkDir = Join-Path $DevRoot 'dotnet'
New-Item -ItemType Directory -Path $downloads,$sdkDir -Force | Out-Null
$sdk = Join-Path $sdkDir 'dotnet.exe'
$sdkVersion = (Get-Content (Join-Path $projectRoot 'global.json') -Raw | ConvertFrom-Json).sdk.version
if (-not (Test-Path -LiteralPath (Join-Path $sdkDir "sdk/$sdkVersion"))) {
    $installer = Join-Path $downloads 'dotnet-install.ps1'
    Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer
    & powershell -NoProfile -ExecutionPolicy Bypass -File $installer -Version $sdkVersion -InstallDir $sdkDir -NoPath
    if ($LASTEXITCODE -ne 0) { throw 'SDK installation failed.' }
}

function Get-VerifiedArchive([string]$Url, [string]$Name, [string]$Hash) {
    $archive = Join-Path $downloads $Name
    if (-not (Test-Path -LiteralPath $archive)) { Invoke-WebRequest $Url -OutFile $archive }
    if ((Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash -ne $Hash) {
        throw "Checksum mismatch: $archive"
    }
    return $archive
}
$bep = Get-VerifiedArchive 'https://thunderstore.io/package/download/denikson/BepInExPack_Valheim/5.4.2351/' 'BepInExPack_Valheim-5.4.2351.zip' 'BCE631497976A93977CEB08E166712E6C31D15244956F89F17DF092A9B62E29F'
$engine = Get-VerifiedArchive 'https://github.com/BepInEx/BepInEx.Debug/releases/download/r11.2/ScriptEngine_r11.2.zip' 'ScriptEngine_r11.2.zip' '6092E582B3F79C518A549503E5F13D65C5D74F0D6381366DD444E592CDE13233'
$bepExtract = Join-Path $downloads 'BepInExPack'
$engineExtract = Join-Path $downloads 'ScriptEngine'
Expand-Archive -LiteralPath $bep -DestinationPath $bepExtract -Force
Expand-Archive -LiteralPath $engine -DestinationPath $engineExtract -Force

# Copy, never mirror/delete. Exclude the original installation's mod loader and logs.
& robocopy $SourceGame $game /E /XD BepInEx /XF winhttp.dll doorstop_config.ini .doorstop_version output_log.txt /NFL /NDL /NJH /NJS /NP
if ($LASTEXITCODE -ge 8) { throw "Game copy failed: $LASTEXITCODE" }
# Preserve any development BepInEx configuration when re-running setup.
$bepConfig = Join-Path $game 'BepInEx/config/BepInEx.cfg'
$previousConfig = if (Test-Path -LiteralPath $bepConfig) { [IO.File]::ReadAllText($bepConfig) } else { $null }
Copy-Item -Path (Join-Path $bepExtract 'BepInExPack_Valheim/*') -Destination $game -Recurse -Force
if ($null -ne $previousConfig) { [IO.File]::WriteAllText($bepConfig, $previousConfig) }
Copy-Item -LiteralPath (Join-Path $engineExtract 'BepInEx/plugins') -Destination (Join-Path $game 'BepInEx') -Recurse -Force
# Preserve existing mod-manager/manual Jotunn installations; doctor checks their version.
$jotunnCopies = @(Get-ChildItem -LiteralPath (Join-Path $game 'BepInEx/plugins') -Recurse -File -Filter 'Jotunn.dll')
if ($jotunnCopies.Count -eq 0) {
    $jotunn = Get-VerifiedArchive 'https://thunderstore.io/package/download/ValheimModding/Jotunn/2.30.2/' 'Jotunn-2.30.2.zip' '8AAE92DA2BE0EB6820CD4CF57E2F6C1D6AD0D738D4915966E7C3D7A94E9A9B0F'
    $jotunnExtract = Join-Path $downloads 'Jotunn-2.30.2'
    Expand-Archive -LiteralPath $jotunn -DestinationPath $jotunnExtract -Force
    $jotunnDestination = Join-Path $game 'BepInEx/plugins/Jotunn'
    New-Item -ItemType Directory -Path $jotunnDestination -Force | Out-Null
    Copy-Item -Path (Join-Path $jotunnExtract 'plugins/*') -Destination $jotunnDestination -Recurse -Force
}
$savePath = Join-Path $DevRoot 'saves'
New-Item -ItemType Directory -Path $savePath,(Join-Path $game 'BepInEx/scripts') -Force | Out-Null
$engineConfig = Join-Path $game 'BepInEx/config/com.bepis.bepinex.scriptengine.cfg'
if (-not (Test-Path -LiteralPath $engineConfig)) {
    Copy-Item -LiteralPath (Join-Path $projectRoot 'config/com.bepis.bepinex.scriptengine.cfg') -Destination $engineConfig
}
@{ gamePath = $game; savePath = $savePath; dotnetPath = $sdk } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $projectRoot 'dev.local.json') -Encoding UTF8
$escapedGame = [Security.SecurityElement]::Escape($game)
"<Project><PropertyGroup><GamePath>$escapedGame</GamePath></PropertyGroup></Project>" |
    Set-Content -LiteralPath (Join-Path $projectRoot 'Environment.props') -Encoding UTF8
& (Join-Path $projectRoot 'dev.ps1') doctor
& (Join-Path $projectRoot 'dev.ps1') deploy
