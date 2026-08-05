param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'Source'
$runtime = Join-Path $source 'KnowledgeV3Runtime.cs'
$persistence = Join-Path $source 'KnowledgePersistenceV3.cs'
$queries = Join-Path $source 'KnowledgeV3Queries.cs'
$verification = Join-Path $source 'KnowledgeVerificationV3.cs'
$transmission = Join-Path $source 'KnowledgeTransmissionV2.cs'
$browser = Join-Path $source 'KnowledgeBrowserV2.cs'
$ui = Join-Path $source 'KnowledgeV3Ui.cs'
$framework = Join-Path $source 'KnowledgeFramework.cs'
$settings = Join-Path $source 'KnowledgeFrameworkSettings.cs'
$definitions = Join-Path $source 'KnowledgeDefinitionsV2.cs'
$schema = Join-Path $source 'KnowledgeSchemaV2.cs'
$discovery = Join-Path $source 'KnowledgeDiscoveryV2.cs'
$transactions = Join-Path $source 'KnowledgeTransactionsV2.cs'
$registry = Join-Path $source 'KnowledgeRegistryV2.cs'
$definitionsV3 = Join-Path $source 'KnowledgeDefinitionsV3.cs'
$integration = Join-Path $root 'INTEGRATION_V2.md'
$mainButton = Join-Path $root '1.6\Defs\MainButtonDefs.xml'
$checks = [ordered]@{
    # These are assembly/resource seams that cannot reasonably run in the
    # out-of-game harness. Runtime behavior is reported by executable suites.
    'V3 source files' = @($runtime, $persistence, $queries, $verification, $transmission, $browser,
        $ui, $framework, $settings, $definitions, $schema, $discovery, $transactions, $registry,
        $definitionsV3 | Where-Object { Test-Path -LiteralPath $_ }).Count -eq 15
    'persistence serialization seam' = (Select-String -Path $persistence -SimpleMatch 'ExposeData' -Quiet) -and
        (Select-String -Path $persistence -SimpleMatch 'knowledgeFrameworkV3Stages' -Quiet)
    'in-game complete-suite entry' = (Select-String -Path (Join-Path $source 'KnowledgeVerificationV2.cs') -SimpleMatch 'Run complete V2/V3 behavioral verification' -Quiet) -and
        (Select-String -Path (Join-Path $source 'KnowledgeVerificationV2.cs') -SimpleMatch 'allowedGameStates = AllowedGameStates.PlayingOnMap' -Quiet)
    'browser/UI resources' = (Test-Path -LiteralPath $mainButton) -and
        (Test-Path -LiteralPath (Join-Path $root '1.6\Languages\English\Keyed\KnowledgeFramework.xml'))
    'behavioral harness present' = Test-Path -LiteralPath (Join-Path $root 'DevTools\BehavioralHarness\KnowledgeFramework.BehavioralHarness.csproj')
}
$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
if ($failed.Count) { throw 'V3 checks failed: ' + (($failed | ForEach-Object { $_.Key }) -join ', ') }
if (-not $SkipBuild) {
    dotnet build (Join-Path $source 'KnowledgeFramework.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Knowledge Framework build failed.' }
}

$behavioral = Join-Path $root 'DevTools\Run-KnowledgeFrameworkBehavioralTests.ps1'
& $behavioral -SkipBuild
if ($LASTEXITCODE -ne 0) { throw 'Knowledge Framework executable behavioral verification failed or was blocked.' }
Write-Output ('Knowledge Framework V3 verification: structural passed={0} failed=0; pure passed=executable; game passed=0 failed=0 unavailable=1 (RimWorld map required); manual UI unavailable=1.' -f $checks.Count)
