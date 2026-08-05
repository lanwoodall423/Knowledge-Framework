param(
    [string]$ManagedPath = $env:RIMWORLD_MANAGED_PATH,
    [string]$HarmonyPath = $env:RIMWORLD_HARMONY_PATH,
    [string]$HarmonyAssemblyPath = $env:RIMWORLD_HARMONY_ASSEMBLY,
    [string]$RimWorldTargetVersion = $env:RIMWORLD_TARGET_VERSION,
    [ValidateSet('Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $root 'Source/KnowledgeFramework.csproj'
$output = Join-Path $root '1.6/Assemblies'
$manifest = Join-Path $output 'KnowledgeFramework.build.json'
$manifestScript = Join-Path $PSScriptRoot 'New-KnowledgeFrameworkBuildManifest.ps1'

if ([string]::IsNullOrWhiteSpace($ManagedPath)) { $ManagedPath = $env:RIMWORLD_MANAGED_ASSEMBLIES }
if ([string]::IsNullOrWhiteSpace($HarmonyAssemblyPath) -and -not [string]::IsNullOrWhiteSpace($HarmonyPath)) {
    if (Test-Path -LiteralPath $HarmonyPath -PathType Leaf) { $HarmonyAssemblyPath = $HarmonyPath }
    else { $HarmonyAssemblyPath = Join-Path $HarmonyPath '0Harmony.dll' }
}
if ([string]::IsNullOrWhiteSpace($RimWorldTargetVersion)) { $RimWorldTargetVersion = '1.6' }

if ([string]::IsNullOrWhiteSpace($ManagedPath) -or -not (Test-Path -LiteralPath $ManagedPath -PathType Container)) {
    $display = if ([string]::IsNullOrWhiteSpace($ManagedPath)) { '<missing>' } else { $ManagedPath }
    throw "build=BLOCKED missing RimWorld Managed directory '$display'. Set -ManagedPath or RIMWORLD_MANAGED_PATH; proprietary assemblies are never downloaded."
}
if ([string]::IsNullOrWhiteSpace($HarmonyAssemblyPath) -or -not (Test-Path -LiteralPath $HarmonyAssemblyPath -PathType Leaf)) {
    $display = if ([string]::IsNullOrWhiteSpace($HarmonyAssemblyPath)) { '<missing>' } else { $HarmonyAssemblyPath }
    throw "build=BLOCKED missing 0Harmony.dll '$display'. Set -HarmonyAssemblyPath/RIMWORLD_HARMONY_ASSEMBLY or -HarmonyPath/RIMWORLD_HARMONY_PATH."
}

$required = @('Assembly-CSharp.dll', 'UnityEngine.CoreModule.dll', 'UnityEngine.IMGUIModule.dll', 'UnityEngine.TextRenderingModule.dll')
foreach ($name in $required) {
    $path = Join-Path $ManagedPath $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "build=BLOCKED missing required RimWorld assembly '$path'." }
}

if (-not (Test-Path -LiteralPath $output -PathType Container)) { New-Item -ItemType Directory -Force -Path $output | Out-Null }
$allowedBefore = @('KnowledgeFramework.dll', 'KnowledgeFramework.build.json')
$unexpected = @(Get-ChildItem -LiteralPath $output -File | Where-Object { $allowedBefore -notcontains $_.Name })
if ($unexpected.Count -gt 0) { throw ('build=FAIL unexpected Release output: {0}' -f (($unexpected | ForEach-Object Name) -join ', ')) }

& dotnet build $project "-c:$Configuration" "-p:KnowledgeFrameworkRimWorldManagedPath=$ManagedPath" "-p:KnowledgeFrameworkHarmonyAssemblyPath=$HarmonyAssemblyPath"
if ($LASTEXITCODE -ne 0) { throw "build=FAIL dotnet build exited with code $LASTEXITCODE." }

$dll = Join-Path $output 'KnowledgeFramework.dll'
if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) { throw "build=FAIL expected primary output is missing: $dll" }
& $manifestScript -AssemblyPath $dll -ManifestPath $manifest -RimWorldTargetVersion $RimWorldTargetVersion
if ($LASTEXITCODE -ne 0) { throw "build=FAIL manifest generation exited with code $LASTEXITCODE." }
& $manifestScript -AssemblyPath $dll -ManifestPath $manifest -RimWorldTargetVersion $RimWorldTargetVersion -Verify
if ($LASTEXITCODE -ne 0) { throw "build=FAIL manifest verification exited with code $LASTEXITCODE." }

$unexpected = @(Get-ChildItem -LiteralPath $output -File | Where-Object { @('KnowledgeFramework.dll', 'KnowledgeFramework.build.json') -notcontains $_.Name })
if ($unexpected.Count -gt 0) { throw ('build=FAIL unexpected Release output after build: {0}' -f (($unexpected | ForEach-Object Name) -join ', ')) }
Write-Output ('build=PASS configuration={0} output={1} manifest={2} target={3}' -f $Configuration, $dll, $manifest, $RimWorldTargetVersion)
