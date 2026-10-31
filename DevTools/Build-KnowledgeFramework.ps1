param(
    [Alias('ManagedPath')] [string]$GameRoot = $env:RIMWORLD_GAME_ROOT,
    [string]$HarmonyPath = $env:RIMWORLD_HARMONY_PATH,
    [string]$HarmonyAssemblyPath = $env:RIMWORLD_HARMONY_ASSEMBLY,
    [string]$RimWorldTargetVersion = $env:RIMWORLD_TARGET_VERSION,
    [ValidateSet('Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $root 'Source/KnowledgeFramework.csproj'
$output = Join-Path $root '1.7/Assemblies'
$manifest = Join-Path $output 'KnowledgeFramework.build.json'
$manifestScript = Join-Path $PSScriptRoot 'New-KnowledgeFrameworkBuildManifest.ps1'

if ([string]::IsNullOrWhiteSpace($GameRoot)) { $GameRoot = $env:RIMWORLD_GAME_ROOT }
if ([string]::IsNullOrWhiteSpace($HarmonyAssemblyPath) -and -not [string]::IsNullOrWhiteSpace($HarmonyPath)) {
    if (Test-Path -LiteralPath $HarmonyPath -PathType Leaf) { $HarmonyAssemblyPath = $HarmonyPath }
    else {
        $versionedHarmony = Join-Path $HarmonyPath '1.7/Assemblies/0Harmony.dll'
        $HarmonyAssemblyPath = if (Test-Path -LiteralPath $versionedHarmony -PathType Leaf) { $versionedHarmony } else { Join-Path $HarmonyPath '0Harmony.dll' }
    }
}
if ([string]::IsNullOrWhiteSpace($RimWorldTargetVersion)) { $RimWorldTargetVersion = '1.7' }
if ($RimWorldTargetVersion -ne '1.7') { throw 'build=BLOCKED RimWorld 1.7 is the only supported target.' }

if ([string]::IsNullOrWhiteSpace($GameRoot) -or -not (Test-Path -LiteralPath $GameRoot -PathType Container)) {
    $display = if ([string]::IsNullOrWhiteSpace($GameRoot)) { '<missing>' } else { $GameRoot }
    throw "build=BLOCKED missing RimWorld 1.7 installation root '$display'. Set -ManagedPath or RIMWORLD_GAME_ROOT; proprietary assemblies are never downloaded."
}
if ([string]::IsNullOrWhiteSpace($HarmonyAssemblyPath) -or -not (Test-Path -LiteralPath $HarmonyAssemblyPath -PathType Leaf)) {
    $display = if ([string]::IsNullOrWhiteSpace($HarmonyAssemblyPath)) { '<missing>' } else { $HarmonyAssemblyPath }
    throw "build=BLOCKED missing 0Harmony.dll '$display'. Set -HarmonyAssemblyPath/RIMWORLD_HARMONY_ASSEMBLY or -HarmonyPath/RIMWORLD_HARMONY_PATH."
}

if (([Reflection.AssemblyName]::GetAssemblyName($HarmonyAssemblyPath)).Version.Major -lt 3) { throw 'build=BLOCKED Harmony 3 is required for RimWorld 1.7.' }

$required = @('Assembly-CSharp.dll', 'UnityStubs.dll', 'Waylith.dll')
foreach ($name in $required) {
    $path = Join-Path $GameRoot $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "build=BLOCKED missing required RimWorld assembly '$path'." }
}

if (-not (Test-Path -LiteralPath $output -PathType Container)) { New-Item -ItemType Directory -Force -Path $output | Out-Null }
$allowedBefore = @('KnowledgeFramework.dll', 'KnowledgeFramework.build.json')
$unexpected = @(Get-ChildItem -LiteralPath $output -File | Where-Object { $allowedBefore -notcontains $_.Name })
if ($unexpected.Count -gt 0) { throw ('build=FAIL unexpected Release output: {0}' -f (($unexpected | ForEach-Object Name) -join ', ')) }

& dotnet build $project "-c:$Configuration" "-p:KnowledgeFrameworkRimWorldManagedPath=$GameRoot" "-p:KnowledgeFrameworkHarmonyAssemblyPath=$HarmonyAssemblyPath"
if ($LASTEXITCODE -ne 0) { throw "build=FAIL dotnet build exited with code $LASTEXITCODE." }

$dll = Join-Path $output 'KnowledgeFramework.dll'
if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) { throw "build=FAIL expected primary output is missing: $dll" }
& $manifestScript -AssemblyPath $dll -ManifestPath $manifest -RimWorldTargetVersion $RimWorldTargetVersion -GameRoot $GameRoot -HarmonyAssemblyPath $HarmonyAssemblyPath -Configuration $Configuration
if ($LASTEXITCODE -ne 0) { throw "build=FAIL manifest generation exited with code $LASTEXITCODE." }
& $manifestScript -AssemblyPath $dll -ManifestPath $manifest -RimWorldTargetVersion $RimWorldTargetVersion -GameRoot $GameRoot -HarmonyAssemblyPath $HarmonyAssemblyPath -Configuration $Configuration -Verify
if ($LASTEXITCODE -ne 0) { throw "build=FAIL manifest verification exited with code $LASTEXITCODE." }

$unexpected = @(Get-ChildItem -LiteralPath $output -File | Where-Object { @('KnowledgeFramework.dll', 'KnowledgeFramework.build.json') -notcontains $_.Name })
if ($unexpected.Count -gt 0) { throw ('build=FAIL unexpected Release output after build: {0}' -f (($unexpected | ForEach-Object Name) -join ', ')) }
Write-Output ('build=PASS configuration={0} output={1} manifest={2} target={3}' -f $Configuration, $dll, $manifest, $RimWorldTargetVersion)
