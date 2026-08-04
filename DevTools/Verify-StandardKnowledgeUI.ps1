param([string]$ModsRoot = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))

$ErrorActionPreference = 'Stop'
$checks = @(
    @{ Path = 'KnowledgeFramework/Source/KnowledgeMenuUI.cs'; Pattern = 'public static class KnowledgeMenuUI'; Name = 'shared UI owner exists' },
    @{ Path = 'KnowledgeFramework/Source/KnowledgeBrowserV2.cs'; Pattern = 'Window_KnowledgeBrowser'; Name = 'browser entry exists' },
    @{ Path = 'AquacultureFishing/Source/AquacultureJournal.cs'; Pattern = 'KnowledgeMenuUI'; Name = 'Aquaculture UI integration source' },
    @{ Path = 'Wildlife/Source/Herds/HuntingKnowledge.cs'; Pattern = 'KnowledgeMenuUI'; Name = 'Wildlife UI integration source' },
    @{ Path = 'Horticulture - Novel Seeds/Source/CultivarRegistry.cs'; Pattern = 'KnowledgeMenuUI'; Name = 'Horticulture UI integration source' },
    @{ Path = 'Horticulture - Novel Seeds/Source/PlantKnowledge.cs'; Pattern = 'KnowledgeService'; Name = 'Horticulture framework integration source' },
    @{ Path = 'AquacultureFishing/Source/FishingExpertise.cs'; Pattern = 'aquacultureFishingProgression'; Name = 'Aquaculture save key declaration' },
    @{ Path = 'Wildlife/Source/Herds/HuntingKnowledge.cs'; Pattern = 'colonistSpeciesKnowledge'; Name = 'Wildlife save key declaration' },
    @{ Path = 'Horticulture - Novel Seeds/Source/ModCore.cs'; Pattern = 'Scribe'; Name = 'Horticulture save integration source' },
    @{ Path = 'KnowledgeFramework/Source/KnowledgeService.cs'; Pattern = 'knowledgeFrameworkColony'; Name = 'framework colony save key declaration' },
    @{ Path = 'KnowledgeFramework/Source/KnowledgeService.cs'; Pattern = 'knowledgeFrameworkExpertise'; Name = 'framework expertise save key declaration' }
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
