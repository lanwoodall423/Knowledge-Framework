param(
    [switch]$SkipBuild,
    [string]$ManagedPath,
    [string]$HarmonyPath
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'DevTools\BehavioralHarness\KnowledgeFramework.BehavioralHarness.csproj'
if ([string]::IsNullOrWhiteSpace($ManagedPath)) {
    $gameRoot = Split-Path (Split-Path $root -Parent) -Parent
    $ManagedPath = Join-Path $gameRoot 'RimWorldWin64_Data\Managed'
}
if (-not (Test-Path -LiteralPath $ManagedPath)) {
    throw ('behavioralSuite=BLOCKED expected=RimWorld managed assemblies actual={0}' -f $ManagedPath)
}
if ([string]::IsNullOrWhiteSpace($HarmonyPath)) {
    $gameRoot = Split-Path (Split-Path $ManagedPath -Parent) -Parent
    $steamAppsPath = Split-Path (Split-Path $gameRoot -Parent) -Parent
    $HarmonyPath = Join-Path $steamAppsPath 'workshop\content\294100\2009463077\Current\Assemblies'
}
if (-not (Test-Path -LiteralPath (Join-Path $HarmonyPath '0Harmony.dll'))) {
    throw ('behavioralSuite=BLOCKED expected=0Harmony.dll actual={0}' -f (Join-Path $HarmonyPath '0Harmony.dll'))
}

if (-not $SkipBuild) {
    dotnet build $project -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Knowledge Framework behavioral harness build failed.' }
}

$previousHarmony = $env:RIMWORLD_HARMONY_PATH
try {
    $env:RIMWORLD_HARMONY_PATH = $HarmonyPath
    dotnet run --project $project -c Release --no-build -- "--managed=$ManagedPath"
    if ($LASTEXITCODE -ne 0) { throw 'Knowledge Framework behavioral suite failed or was blocked.' }
}
finally {
    $env:RIMWORLD_HARMONY_PATH = $previousHarmony
}
