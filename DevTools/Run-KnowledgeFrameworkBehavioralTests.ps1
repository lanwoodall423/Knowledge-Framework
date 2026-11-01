param(
    [switch]$SkipBuild,
    [Alias('ManagedPath')] [string]$GameRoot = $env:RIMWORLD_GAME_ROOT,
    [string]$HarmonyPath = $env:RIMWORLD_HARMONY_PATH,
    [string]$AssemblyPath
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'DevTools/BehavioralHarness/KnowledgeFramework.BehavioralHarness.csproj'
if ([string]::IsNullOrWhiteSpace($GameRoot) -or -not (Test-Path -LiteralPath $GameRoot -PathType Container)) {
    $managedDisplay = if ([string]::IsNullOrWhiteSpace($GameRoot)) { '<missing>' } else { $GameRoot }
    throw ('behavioralSuite=BLOCKED expected=RimWorld managed assemblies actual={0}; set -ManagedPath or RIMWORLD_GAME_ROOT' -f $managedDisplay)
}
if ([string]::IsNullOrWhiteSpace($HarmonyPath)) {
    $HarmonyPath = $env:RIMWORLD_HARMONY_ASSEMBLIES
}
$harmonyAssembly = if ([string]::IsNullOrWhiteSpace($HarmonyPath)) { $null } elseif ((Test-Path -LiteralPath $HarmonyPath -PathType Leaf)) { $HarmonyPath } else { $versioned = Join-Path $HarmonyPath '1.7/Assemblies/0Harmony.dll'; if (Test-Path -LiteralPath $versioned -PathType Leaf) { $versioned } else { Join-Path $HarmonyPath '0Harmony.dll' } }
if ([string]::IsNullOrWhiteSpace($harmonyAssembly) -or -not (Test-Path -LiteralPath $harmonyAssembly -PathType Leaf)) {
    $harmonyDisplay = if ([string]::IsNullOrWhiteSpace($harmonyAssembly)) { '<missing>' } else { $harmonyAssembly }
    throw ('behavioralSuite=BLOCKED expected=0Harmony.dll actual={0}; set -HarmonyPath or RIMWORLD_HARMONY_PATH' -f $harmonyDisplay)
}
if (([Reflection.AssemblyName]::GetAssemblyName($harmonyAssembly)).Version.Major -lt 3) { throw 'behavioralSuite=BLOCKED Harmony 3 required.' }
$harmonyDirectory = Split-Path -Parent $harmonyAssembly

if (-not $SkipBuild) {
    dotnet build $project -c Release "-p:KnowledgeFrameworkRimWorldManagedPath=$GameRoot" "-p:KnowledgeFrameworkHarmonyAssemblyPath=$harmonyAssembly"
    if ($LASTEXITCODE -ne 0) { throw 'Knowledge Framework behavioral harness build failed.' }
}

$previousHarmony = $env:RIMWORLD_HARMONY_PATH
try {
    $env:RIMWORLD_HARMONY_PATH = $harmonyDirectory
    $arguments = @('--game-root=' + $GameRoot)
    if (-not [string]::IsNullOrWhiteSpace($AssemblyPath)) { $arguments += '--assembly=' + $AssemblyPath }
    & dotnet run --project $project -c Release --no-build -- @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Knowledge Framework behavioral suite failed or was blocked.' }
}
finally {
    $env:RIMWORLD_HARMONY_PATH = $previousHarmony
}
