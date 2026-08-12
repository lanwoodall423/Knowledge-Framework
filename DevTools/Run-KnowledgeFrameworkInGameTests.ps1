param(
    [string]$ManagedPath,
    [string]$HarmonyAssemblyPath,
    [int]$TimeoutSeconds = 300,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$gameRoot = [IO.Path]::GetFullPath((Join-Path $root '..\..'))
$bridgeRoot = Join-Path $gameRoot 'Mods\DevBridge2'
$bridge = Join-Path $bridgeRoot 'DevBridge.cmd'
$buildScript = Join-Path $root 'DevTools\Build-KnowledgeFramework.ps1'
$configPath = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'AppData\LocalLow\Ludeon Studios\RimWorld by Ludeon Studios\Config'
$requestPath = Join-Path $configPath 'KnowledgeFramework_AutomaticTest.request'
$reportPath = Join-Path $configPath 'KnowledgeFramework_AutomaticTest.txt'

if (-not (Test-Path -LiteralPath $bridge -PathType Leaf)) { throw "DevBridge2 coordinator is missing: $bridge" }
if ([string]::IsNullOrWhiteSpace($ManagedPath)) {
    $ManagedPath = Join-Path $gameRoot 'RimWorldWin64_Data\Managed'
}
if ([string]::IsNullOrWhiteSpace($HarmonyAssemblyPath)) {
    $HarmonyAssemblyPath = Join-Path $gameRoot '..\..\workshop\content\294100\2009463077\Current\Assemblies\0Harmony.dll'
}
$ManagedPath = [IO.Path]::GetFullPath($ManagedPath)
$HarmonyAssemblyPath = [IO.Path]::GetFullPath($HarmonyAssemblyPath)

if (-not $SkipBuild) {
    & $buildScript -ManagedPath $ManagedPath -HarmonyAssemblyPath $HarmonyAssemblyPath -RimWorldTargetVersion '1.6'
    if ($LASTEXITCODE -ne 0) { throw "Knowledge Framework build failed with exit code $LASTEXITCODE." }
}

New-Item -ItemType Directory -Force -Path $configPath | Out-Null
foreach ($path in @($requestPath, $reportPath, $reportPath + '.tmp')) {
    if (Test-Path -LiteralPath $path -PathType Leaf) { Remove-Item -LiteralPath $path -Force }
}
$runId = [Guid]::NewGuid().ToString('N')
[IO.File]::WriteAllText($requestPath, "run-id=$runId`nrequested-utc=$([DateTime]::UtcNow.ToString('o'))`n")

$leaseId = $null
try {
    Write-Output 'Requesting a fresh RimWorld generation through DevBridge2...'
    & $bridge 'restart'
    if ($LASTEXITCODE -ne 0) { throw "DevBridge2 restart failed with exit code $LASTEXITCODE." }

    $beginOutput = @(& $bridge 'test' 'begin' 2>&1)
    $beginExitCode = $LASTEXITCODE
    $beginText = $beginOutput -join "`n"
    Write-Output $beginText
    if ($beginExitCode -ne 0) { throw "DevBridge2 test lease acquisition failed with exit code $beginExitCode." }
    $leaseMatch = [regex]::Match($beginText, '(?im)^Test lease acquired:\s*(\S+)\s*$')
    if (-not $leaseMatch.Success) { throw 'DevBridge2 did not print a test lease ID.' }
    $leaseId = $leaseMatch.Groups[1].Value

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $report = $null
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $reportPath -PathType Leaf) {
            try {
                $candidate = [IO.File]::ReadAllText($reportPath)
                if ($candidate -match '(?im)^completed=true\s*$') {
                    $report = $candidate
                    break
                }
            }
            catch { }
        }
        Start-Sleep -Milliseconds 500
    }
    if ($null -eq $report) { throw "Timed out waiting for the mod-owned report: $reportPath" }
    Write-Output $report
    if ($report -notmatch '(?im)^status=PASS\s*$') { throw 'Knowledge Framework automatic in-game tests reported FAIL.' }
}
finally {
    if (-not [string]::IsNullOrWhiteSpace($leaseId)) {
        & $bridge 'test' 'end' $leaseId
        if ($LASTEXITCODE -ne 0) { Write-Warning "DevBridge2 test lease cleanup failed with exit code $LASTEXITCODE." }
    }
}
