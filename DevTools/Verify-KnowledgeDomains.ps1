$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$domain = Get-Content -Raw (Join-Path $root 'Source\KnowledgeDomains.cs')
$service = Get-Content -Raw (Join-Path $root 'Source\KnowledgeService.cs')

$checks = [ordered]@{
    'versioned capability API' = $domain -match 'ApiVersion = 1' -and $domain -match 'Supports\(int minimumApiVersion'
    'domain neutral definitions' = $domain -match 'KnowledgeDomainDefinition' -and $domain -match 'KnowledgeSubjectDefinition'
    'expertise can be disabled' = $domain -match 'bool expertiseEnabled' -and $service -match 'domain\.expertiseEnabled'
    'colony records cannot reference pawns' = $service -match 'class ColonyKnowledgeSaveRecord' -and $service -match 'class PawnKnowledgeSaveRecord : ColonyKnowledgeSaveRecord'
    'immutable query snapshots' = $domain -match 'sealed class KnowledgeSnapshot' -and $domain -match 'readonly float experience'
    'framework service owns mutation' = $service -match 'public static bool Award\(KnowledgeAward award\)'
    'hot queries are scalar and allocation light' = $service -match 'GetPawnKnowledgeExperience' -and $service -match 'GetPawnKnowledgeRank' -and $domain -match 'readonly struct KnowledgeEffectContext'
    'legacy import is idempotent' = $service -match 'ImportMinimum' -and $service -match 'Mathf\.Max\(personal\.experience, pawnExperience\)'
    'reveal and effect providers' = $domain -match 'IKnowledgeEffectProvider' -and $service -match 'MeetsReveal' -and $service -match 'ApplyEffects'
    'knowledge change subscription' = $service -match 'event Action<KnowledgeChangedEvent> KnowledgeChanged'
    'separate save keys' = $service -match 'knowledgeFrameworkColony' -and $service -match 'knowledgeFrameworkPawns' -and $service -match 'knowledgeFrameworkExpertise'
    'orphan validation retained' = $service -match 'Orphaned colony subject' -and $service -match 'Orphaned pawn subject'
    'debug lifecycle tools' = $service -match 'List domains and subjects' -and $service -match 'Inspect selected pawn' -and $service -match 'Award selected pawn 100 knowledge' -and $service -match 'Reset selected pawn knowledge'
}
$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
if ($failed.Count) { throw 'Knowledge domain checks failed: ' + (($failed | ForEach-Object Key) -join ', ') }
Write-Output ("Knowledge domain verification passed ({0} checks)." -f $checks.Count)
