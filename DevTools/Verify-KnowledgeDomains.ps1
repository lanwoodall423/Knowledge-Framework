$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$domainPath = Join-Path $root 'Source\KnowledgeDomains.cs'
$servicePath = Join-Path $root 'Source\KnowledgeService.cs'
$persistencePath = Join-Path $root 'Source\KnowledgePersistenceV2.cs'

$checks = [ordered]@{
    'domain source and public model exist' = (Test-Path $domainPath) -and (Select-String -Path $domainPath -SimpleMatch 'class KnowledgeDomainDefinition' -Quiet)
    'domain service entry point exists' = (Test-Path $servicePath) -and (Select-String -Path $servicePath -SimpleMatch 'public static bool Award' -Quiet)
    'domain capability entry point exists' = (Select-String -Path $domainPath -SimpleMatch 'EvidenceCapability' -Quiet)
    'domain snapshot model exists' = (Select-String -Path $domainPath -SimpleMatch 'class KnowledgeSnapshot' -Quiet)
    'domain effect provider contract exists' = (Select-String -Path $domainPath -SimpleMatch 'IKnowledgeEffectProvider' -Quiet)
    'knowledge change event exists' = (Select-String -Path $servicePath -SimpleMatch 'KnowledgeChanged' -Quiet)
    'V2 persistence source exists' = Test-Path $persistencePath
    'domain save keys are declared' = (Select-String -Path $servicePath -SimpleMatch 'knowledgeFrameworkColony' -Quiet) -and
        (Select-String -Path $servicePath -SimpleMatch 'knowledgeFrameworkPawns' -Quiet) -and
        (Select-String -Path $servicePath -SimpleMatch 'knowledgeFrameworkExpertise' -Quiet)
    'domain debug entry points exist' = (Select-String -Path $servicePath -SimpleMatch 'List domains and subjects' -Quiet) -and
        (Select-String -Path $servicePath -SimpleMatch 'Inspect selected pawn' -Quiet)
    'V2 verification source exists' = Test-Path (Join-Path $root 'Source\KnowledgeVerificationV2.cs')
}
$failed = @($checks.GetEnumerator() | Where-Object { -not $_.Value })
if ($failed.Count) { throw 'Knowledge domain checks failed: ' + (($failed | ForEach-Object Key) -join ', ') }
Write-Output ("Knowledge domain verification passed ({0} checks)." -f $checks.Count)
