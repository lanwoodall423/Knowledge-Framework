param([string]$ModsRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))

$ErrorActionPreference = 'Stop'
$checks = @(
    @{ Path = 'KnowledgeFramework/Source/KnowledgeMenuUI.cs'; Pattern = 'public static class KnowledgeMenuUI'; Name = 'shared detailed UI owner' },
    @{ Path = 'KnowledgeFramework/Source/KnowledgeMenuUI.cs'; Pattern = 'KnowledgeMenuScope.Colony'; Name = 'shared Colony scope' },
    @{ Path = 'KnowledgeFramework/Source/KnowledgeMenuUI.cs'; Pattern = 'Widgets.TextField'; Name = 'shared search control' },
    @{ Path = 'KnowledgeFramework/Source/KnowledgeMenuUI.cs'; Pattern = 'OrderBy(row => row.label)'; Name = 'stable alphabetical sorting' },
    @{ Path = 'AquacultureFishing/Source/AquacultureJournal.cs'; Pattern = 'KnowledgeMenuUI.Draw'; Name = 'Aquaculture adapter' },
    @{ Path = 'AquacultureFishing/Source/AquacultureJournal.cs'; Pattern = 'Mathf.Clamp01(records.Sum'; Name = 'Aquaculture colony knowledge accumulation' },
    @{ Path = 'Wildlife/Source/Herds/HuntingKnowledge.cs'; Pattern = 'KnowledgeMenuUI.Draw'; Name = 'Wildlife adapter' },
    @{ Path = 'Wildlife/Source/Herds/HuntingKnowledge.cs'; Pattern = 'group.Sum(record => record.experience)'; Name = 'Wildlife biome accumulation' },
    @{ Path = 'Wildlife/Source/Herds/HuntingKnowledge.cs'; Pattern = 'ColonyBiomeRecords'; Name = 'Wildlife all-map biome source' },
    @{ Path = 'Horticulture - Novel Seeds/Source/CultivarRegistry.cs'; Pattern = 'KnowledgeMenuUI.Draw'; Name = 'Horticulture adapter' },
    @{ Path = 'Horticulture - Novel Seeds/Source/CultivarRegistry.cs'; Pattern = 'AggregateKnowledge(group)'; Name = 'Horticulture colony accumulation' },
    @{ Path = 'AquacultureFishing/Source/FishingExpertise.cs'; Pattern = 'aquacultureFishingProgression'; Name = 'Aquaculture legacy save key' },
    @{ Path = 'Wildlife/Source/Herds/HuntingKnowledge.cs'; Pattern = 'colonistSpeciesKnowledge'; Name = 'Wildlife legacy save key' },
    @{ Path = 'Horticulture - Novel Seeds/Source/ModCore.cs'; Pattern = 'horticultureKnowledge'; Name = 'Horticulture legacy save key' }
)

$failed = @()
foreach ($check in $checks) {
    $path = Join-Path $ModsRoot $check.Path
    if (!(Test-Path $path) -or !(Select-String -Path $path -SimpleMatch $check.Pattern -Quiet)) {
        $failed += $check.Name
    }
}

if ($failed.Count -gt 0) {
    throw 'Standard Knowledge UI checks failed: ' + ($failed -join ', ')
}

Write-Host ('Standard Knowledge UI checks passed ({0} checks).' -f $checks.Count)
