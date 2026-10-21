param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
$local = Get-Content -LiteralPath (Join-Path $root 'dev.local.json') -Raw | ConvertFrom-Json
$game = [IO.Path]::GetFullPath($local.gamePath)
$null = [Reflection.Assembly]::LoadFrom((Join-Path $game 'BepInEx/core/Mono.Cecil.dll'))
$resolver = New-Object Mono.Cecil.DefaultAssemblyResolver
try {
    $resolver.AddSearchDirectory((Join-Path $game 'BepInEx/core'))
    $resolver.AddSearchDirectory((Join-Path $game 'valheim_Data/Managed'))
    # Match ScriptEngine's eager metadata read without making nested plugin DLLs
    # available. A Jotunn enum-valued attribute breaks this development path.
    if ($Configuration -eq 'Release') {
        $copies = @(Get-ChildItem -LiteralPath (Join-Path $game 'BepInEx/plugins') -Recurse -Filter Jotunn.dll -File)
        if ($copies.Count -ne 1) { throw 'Expected exactly one installed Jotunn.dll.' }
        $resolver.AddSearchDirectory($copies[0].DirectoryName)
    }
    $parameters = New-Object Mono.Cecil.ReaderParameters
    $parameters.AssemblyResolver = $resolver
    $parameters.ReadingMode = [Mono.Cecil.ReadingMode]::Immediate
    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly(
        (Join-Path $root "src/Ragnarok/bin/$Configuration/net472/Ragnarok.dll"), $parameters)
    try {
        $plugin = $assembly.MainModule.Types | Where-Object FullName -eq 'Ragnarok.Plugin'
        $dependencies = @($plugin.CustomAttributes | Where-Object {
            $_.AttributeType.FullName -eq 'BepInEx.BepInDependency' -and
            $_.ConstructorArguments[0].Value -eq 'com.jotunn.jotunn'
        })
        if ($dependencies.Count -ne 1 -or $dependencies[0].ConstructorArguments[1].Value -ne '2.30.2') {
            throw 'Required Jotunn dependency metadata changed.'
        }
        $compatibility = @($plugin.CustomAttributes | Where-Object {
            $_.AttributeType.FullName -eq 'Jotunn.Utils.NetworkCompatibilityAttribute'
        })
        if ($Configuration -eq 'Debug') {
            if ($compatibility.Count -ne 0) { throw 'Development plugin must omit the Jotunn enum attribute.' }
        } else {
            if ($compatibility.Count -ne 1) { throw 'Release compatibility metadata is missing.' }
            $arguments = $compatibility[0].ConstructorArguments
            foreach ($expected in @('VersionCheckOnly', 'Minor')) {
                $index = if ($expected -eq 'VersionCheckOnly') { 0 } else { 1 }
                $enum = $arguments[$index].Type.Resolve()
                $field = $enum.Fields | Where-Object Name -eq $expected
                if ($arguments[$index].Value -ne $field.Constant) { throw "Expected $expected release policy." }
            }
        }
        Write-Host "PASS: $Configuration eager metadata read, required Jotunn dependency and compatibility policy."
    } finally { $assembly.Dispose() }
} finally { $resolver.Dispose() }
