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
$integration = Join-Path $root 'INTEGRATION_V2.md'
$mainButton = Join-Path $root '1.6\Defs\MainButtonDefs.xml'
$checks = [ordered]@{
    'global context chain' = (Select-String -Path $runtime -SimpleMatch 'KnowledgeContextKey.Empty' -Quiet) -and
        (Select-String -Path $runtime -SimpleMatch 'KnowledgeContextRegistry.Chain' -Quiet)
    'global claims and query paths' = (Select-String -Path $runtime -SimpleMatch 'ForSubject(' -Quiet) -and
        (Select-String -Path $queries -SimpleMatch 'KnowledgeClaimService.ForSubject' -Quiet)
    'bounded accrual accounting' = (Select-String -Path $runtime -SimpleMatch 'dailyCap' -Quiet) -and
        (Select-String -Path $runtime -SimpleMatch 'plannedContexts' -Quiet) -and
        (Select-String -Path $runtime -SimpleMatch 'EnforceStateLimit' -Quiet)
    'accrual persistence ownership' = (Select-String -Path $persistence -SimpleMatch 'policyNamespace' -Quiet) -and
        (Select-String -Path $persistence -SimpleMatch 'RemoveAccrualV3' -Quiet) -and
        (Select-String -Path $persistence -SimpleMatch 'NormalizeAccrual' -Quiet)
    'context propagation control' = (Select-String -Path $runtime -SimpleMatch 'propagateContext' -Quiet) -and
        (Select-String -Path $runtime -SimpleMatch 'ContextPropagates' -Quiet)
    'milestone transmission control' = (Select-String -Path $transmission -SimpleMatch 'includeMilestones' -Quiet) -and
        (Select-String -Path $transmission -SimpleMatch 'CopyMilestones' -Quiet)
    'V3 regression coverage' = (Select-String -Path $verification -SimpleMatch 'global personal claim apply/query' -Quiet) -and
        (Select-String -Path $verification -SimpleMatch 'daily cap boundary' -Quiet) -and
        (Select-String -Path $verification -SimpleMatch 'save/index rebuild normalization' -Quiet)
    'stage aggregation policy' = (Select-String -Path $definitions -SimpleMatch 'KnowledgeStageAggregationMode' -Quiet) -and
        (Select-String -Path $schema -SimpleMatch 'stageAggregationMode' -Quiet) -and
        (Select-String -Path $integration -SimpleMatch 'LegacySumMax' -Quiet) -and
        (Select-String -Path $integration -SimpleMatch 'Balanced' -Quiet)
    'stage aggregation regressions' = (Select-String -Path $verification -SimpleMatch 'balanced equivalent stage eligibility' -Quiet) -and
        (Select-String -Path $verification -SimpleMatch 'empty facet cannot advance stage' -Quiet) -and
        (Select-String -Path $verification -SimpleMatch 'stage survives save/index reconstruction' -Quiet)
    'stage aggregation numerical modes' = (Select-String -Path $verification -SimpleMatch 'legacy stage aggregation compatibility' -Quiet) -and
        (Select-String -Path $verification -SimpleMatch 'balanced confidence requires broad support' -Quiet) -and
        (Select-String -Path $verification -SimpleMatch 'balanced empty facet does not advance' -Quiet)
    'browser context propagation' = (Select-String -Path $browser -SimpleMatch 'RequestedContext' -Quiet) -and
        (Select-String -Path $browser -SimpleMatch 'BuildSubject' -Quiet) -and
        (Select-String -Path $ui -SimpleMatch 'requestedContext' -Quiet)
    'browser discovery filtering' = (Select-String -Path $ui -SimpleMatch 'KnowledgeDiscovery.Present' -Quiet) -and
        (Select-String -Path $ui -SimpleMatch 'revealedByDefault' -Quiet) -and
        (Select-String -Path $ui -SimpleMatch 'developerMode' -Quiet)
    'browser localized labels' = (Select-String -Path $ui -SimpleMatch 'KnowledgeBrowserLabels' -Quiet) -and
        (Select-String -Path $browser -SimpleMatch 'KnowledgeBrowserLabels.Stage' -Quiet)
    'browser hidden information' = (Select-String -Path $ui -SimpleMatch 'includeHidden' -Quiet) -and
        (Select-String -Path $ui -SimpleMatch 'revealedByDefault' -Quiet) -and
        (Select-String -Path $ui -SimpleMatch 'claims = facets' -Quiet)
    'targeted detail cache' = (Select-String -Path $browser -SimpleMatch 'detailKnowledgeRevision' -Quiet) -and
        (Select-String -Path $browser -SimpleMatch 'detailUiRevision' -Quiet) -and
        (Select-String -Path $browser -SimpleMatch 'KnowledgeBrowserModels.BuildSubject' -Quiet) -and
        -not (Select-String -Path $browser -SimpleMatch 'KnowledgeBrowserModels.Build(' -Quiet)
    'context-aware cache keys' = (Select-String -Path $browser -SimpleMatch 'detailContext' -Quiet) -and
        (Select-String -Path $browser -SimpleMatch 'modelContext' -Quiet)
    'contextual provider support' = Select-String -Path $browser -SimpleMatch 'IKnowledgeDomainUiV2Contextual' -Quiet
    'Bio setting integration' = (Select-String -Path $settings -SimpleMatch 'BioPanelEnabled' -Quiet) -and
        (Select-String -Path $framework -SimpleMatch 'BioPanelEnabled' -Quiet) -and
        (Select-String -Path $framework -SimpleMatch 'if (!VisibleFor' -Quiet)
    'colony browser entry' = (Test-Path -LiteralPath $mainButton) -and
        (Select-String -Path $ui -SimpleMatch 'OpenColony' -Quiet)
}
$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
if ($failed.Count) { throw 'V3 checks failed: ' + (($failed | ForEach-Object { $_.Key }) -join ', ') }
if (-not $SkipBuild) {
    dotnet build (Join-Path $source 'KnowledgeFramework.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Knowledge Framework build failed.' }
}
Write-Output ('Knowledge Framework V3 verification passed ({0} checks).' -f $checks.Count)
