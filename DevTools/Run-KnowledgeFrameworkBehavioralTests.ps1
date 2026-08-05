param(
    [switch]$SkipBuild,
    [string]$ManagedPath = $env:RIMWORLD_MANAGED_PATH,
    [string]$HarmonyPath = $env:RIMWORLD_HARMONY_PATH,
    [string]$AssemblyPath
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'DevTools/BehavioralHarness/KnowledgeFramework.BehavioralHarness.csproj'
if ([string]::IsNullOrWhiteSpace($ManagedPath) -or -not (Test-Path -LiteralPath $ManagedPath -PathType Container)) {
    $managedDisplay = if ([string]::IsNullOrWhiteSpace($ManagedPath)) { '<missing>' } else { $ManagedPath }
    throw ('behavioralSuite=BLOCKED expected=RimWorld managed assemblies actual={0}; set -ManagedPath or RIMWORLD_MANAGED_PATH' -f $managedDisplay)
}
if ([string]::IsNullOrWhiteSpace($HarmonyPath)) {
    $HarmonyPath = $env:RIMWORLD_HARMONY_ASSEMBLIES
}
$harmonyAssembly = if ([string]::IsNullOrWhiteSpace($HarmonyPath)) { $null } elseif ((Test-Path -LiteralPath $HarmonyPath -PathType Leaf)) { $HarmonyPath } else { Join-Path $HarmonyPath '0Harmony.dll' }
if ([string]::IsNullOrWhiteSpace($harmonyAssembly) -or -not (Test-Path -LiteralPath $harmonyAssembly -PathType Leaf)) {
    $harmonyDisplay = if ([string]::IsNullOrWhiteSpace($harmonyAssembly)) { '<missing>' } else { $harmonyAssembly }
    throw ('behavioralSuite=BLOCKED expected=0Harmony.dll actual={0}; set -HarmonyPath or RIMWORLD_HARMONY_PATH' -f $harmonyDisplay)
}
$harmonyDirectory = Split-Path -Parent $harmonyAssembly

if (-not $SkipBuild) {
    dotnet build $project -c Release "-p:KnowledgeFrameworkRimWorldManagedPath=$ManagedPath" "-p:KnowledgeFrameworkHarmonyAssemblyPath=$harmonyAssembly"
    if ($LASTEXITCODE -ne 0) { throw 'Knowledge Framework behavioral harness build failed.' }
}

$previousHarmony = $env:RIMWORLD_HARMONY_PATH
try {
    $env:RIMWORLD_HARMONY_PATH = $harmonyDirectory
    $arguments = @('--managed=' + $ManagedPath)
    if (-not [string]::IsNullOrWhiteSpace($AssemblyPath)) { $arguments += '--assembly=' + $AssemblyPath }
    & dotnet run --project $project -c Release --no-build -- @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Knowledge Framework behavioral suite failed or was blocked.' }
}
finally {
    $env:RIMWORLD_HARMONY_PATH = $previousHarmony
}
