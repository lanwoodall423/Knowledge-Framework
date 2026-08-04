param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'Source'
$checks = [ordered]@{
    'V2 definitions exist' = (Test-Path (Join-Path $source 'KnowledgeDefinitionsV2.cs')) -and
        (Select-String -Path (Join-Path $source 'KnowledgeDefinitionsV2.cs') -SimpleMatch 'class KnowledgeDomainDef' -Quiet)
    'V2 persistence and transaction entry points exist' = (Test-Path (Join-Path $source 'KnowledgePersistenceV2.cs')) -and
        (Test-Path (Join-Path $source 'KnowledgeTransactionsV2.cs')) -and
        (Select-String -Path (Join-Path $source 'KnowledgeTransactionsV2.cs') -SimpleMatch 'class KnowledgeTransaction' -Quiet)
    'V2 registry and effects entry points exist' = (Test-Path (Join-Path $source 'KnowledgeRegistryV2.cs')) -and
        (Test-Path (Join-Path $source 'KnowledgeInsightsEffectsV2.cs')) -and
        (Select-String -Path (Join-Path $source 'KnowledgeInsightsEffectsV2.cs') -SimpleMatch 'IKnowledgeTypedEffectProvider' -Quiet)
    'V2 service and browser entry points exist' = (Test-Path (Join-Path $source 'KnowledgeService.cs')) -and
        (Test-Path (Join-Path $source 'KnowledgeBrowserV2.cs')) -and
        (Test-Path (Join-Path $source 'KnowledgeMenuUI.cs'))
    'V2 verification entry points exist' = (Select-String -Path (Join-Path $source 'KnowledgeVerificationV2.cs') -SimpleMatch 'RunPureTests' -Quiet) -and
        (Select-String -Path (Join-Path $source 'KnowledgeVerificationV2.cs') -SimpleMatch 'RunGameTests' -Quiet)
    'V2 developer action exists' = (Select-String -Path (Join-Path $source 'KnowledgeVerificationV2.cs') -SimpleMatch 'RunFromDebugMenu' -Quiet)
    'V2 persistence keys are declared' = (Select-String -Path (Join-Path $source 'KnowledgePersistenceV2.cs') -SimpleMatch 'personalFacetsV2' -Quiet) -and
        (Select-String -Path (Join-Path $source 'KnowledgePersistenceV2.cs') -SimpleMatch 'RebuildV2Indexes' -Quiet)
    'executable behavioral harness exists' = Test-Path (Join-Path $PSScriptRoot 'BehavioralHarness\KnowledgeFramework.BehavioralHarness.csproj')
}
$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
if ($failed.Count) { throw 'V2 checks failed: ' + (($failed | ForEach-Object Key) -join ', ') }
if (-not $SkipBuild) {
    dotnet build (Join-Path $source 'KnowledgeFramework.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Knowledge Framework build failed.' }
}
$behavioral = Join-Path $PSScriptRoot 'Run-KnowledgeFrameworkBehavioralTests.ps1'
& $behavioral -SkipBuild
if (-not $?) { throw 'Executable behavioral verification failed.' }
Write-Output ('Knowledge Framework V2 verification passed ({0} structural checks + executable pure suite; game suite requires a RimWorld map).' -f $checks.Count)
