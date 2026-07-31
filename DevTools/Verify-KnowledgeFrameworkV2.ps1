param([switch]$SkipBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'Source'
$checks = [ordered]@{
    'schema definitions' = (Test-Path (Join-Path $source 'KnowledgeDefinitionsV2.cs')) -and
        (Select-String -Path (Join-Path $source 'KnowledgeDefinitionsV2.cs') -SimpleMatch 'class KnowledgeDomainDef' -Quiet) -and
        (Select-String -Path (Join-Path $source 'KnowledgeDefinitionsV2.cs') -SimpleMatch 'class KnowledgeFacetDef' -Quiet)
    'indexed V2 persistence' = (Select-String -Path (Join-Path $source 'KnowledgePersistenceV2.cs') -SimpleMatch 'personalFacetsV2' -Quiet) -and
        (Select-String -Path (Join-Path $source 'KnowledgePersistenceV2.cs') -SimpleMatch 'RebuildV2Indexes' -Quiet)
    'atomic observations' = (Select-String -Path (Join-Path $source 'KnowledgeTransactionsV2.cs') -SimpleMatch 'class KnowledgeTransaction' -Quiet) -and
        (Select-String -Path (Join-Path $source 'KnowledgeTransactionsV2.cs') -SimpleMatch 'TryValidate' -Quiet)
    'bounded evidence' = (Select-String -Path (Join-Path $source 'KnowledgeTransactionsV2.cs') -SimpleMatch 'evidenceAggregateLimit' -Quiet) -and
        (Select-String -Path (Join-Path $source 'KnowledgePersistenceV2.cs') -SimpleMatch 'BoundFacet' -Quiet)
    'insights and relationships' = (Select-String -Path (Join-Path $source 'KnowledgeInsightsEffectsV2.cs') -SimpleMatch 'EvaluateTouched' -Quiet) -and
        (Select-String -Path (Join-Path $source 'KnowledgeRegistryV2.cs') -SimpleMatch 'HasRelationshipCycle' -Quiet) -and
        (Select-String -Path (Join-Path $source 'KnowledgeRegistryV2.cs') -SimpleMatch 'HasInsightCycle' -Quiet)
    'typed effects' = (Select-String -Path (Join-Path $source 'KnowledgeInsightsEffectsV2.cs') -SimpleMatch 'IKnowledgeTypedEffectProvider' -Quiet) -and
        (Select-String -Path (Join-Path $source 'KnowledgeInsightsEffectsV2.cs') -SimpleMatch 'KnowledgeEffectComposition' -Quiet)
    'compatibility facade' = (Select-String -Path (Join-Path $source 'KnowledgeService.cs') -SimpleMatch 'KnowledgeEngine.Submit' -Quiet) -and
        (Select-String -Path (Join-Path $source 'KnowledgePersistenceV2.cs') -SimpleMatch 'MigrateLegacyV1' -Quiet)
    'shared V2 browser' = (Select-String -Path (Join-Path $source 'KnowledgeBrowserV2.cs') -SimpleMatch 'Window_KnowledgeBrowser' -Quiet) -and
        (Select-String -Path (Join-Path $source 'KnowledgeMenuUI.cs') -SimpleMatch 'BeginScrollView' -Quiet)
    'verification suite' = (Select-String -Path (Join-Path $source 'KnowledgeVerificationV2.cs') -SimpleMatch 'RunPureTests' -Quiet) -and
        (Select-String -Path (Join-Path $source 'KnowledgeVerificationV2.cs') -SimpleMatch 'RunGameTests' -Quiet)
}
$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
if ($failed.Count) { throw 'V2 checks failed: ' + (($failed | ForEach-Object Key) -join ', ') }
if (-not $SkipBuild) {
    dotnet build (Join-Path $source 'KnowledgeFramework.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Knowledge Framework build failed.' }
}
Write-Output ('Knowledge Framework V2 verification passed ({0} checks).' -f $checks.Count)
