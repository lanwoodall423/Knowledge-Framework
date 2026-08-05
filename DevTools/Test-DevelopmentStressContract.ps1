param(
    [string]$Root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
)

$ErrorActionPreference = 'Stop'
$stressPath = Join-Path $Root 'Source/Development/KnowledgeFrameworkStressVerification.cs'
$runtimePath = Join-Path $Root 'RUNTIME_TESTING.md'
$errors = New-Object Collections.Generic.List[string]

if (-not (Test-Path -LiteralPath $stressPath -PathType Leaf)) { $errors.Add('stress source is missing') | Out-Null }
else {
    $source = Get-Content -LiteralPath $stressPath -Raw
    foreach ($required in @(
        'namespace KnowledgeFramework.Development',
        '[DebugAction(',
        'SubjectCount = 5000',
        'RequiredPawns = 20',
        'KnowledgeEngine.Submit',
        'KnowledgeClaimService.Snapshot',
        'RebuildV3Indexes',
        'RemoveDomainDataV2',
        'KnowledgeFramework_Stress.txt',
        'ApproximatePersistentBytes'
    )) {
        if ($source.IndexOf($required, [StringComparison]::Ordinal) -lt 0) { $errors.Add('stress source missing ' + $required) | Out-Null }
    }
}
if (-not (Test-Path -LiteralPath $runtimePath -PathType Leaf)) { $errors.Add('RUNTIME_TESTING.md is missing') | Out-Null }
elseif ((Get-Content -LiteralPath $runtimePath -Raw).IndexOf('bounded V3 stress validation', [StringComparison]::OrdinalIgnoreCase) -lt 0) {
    $errors.Add('runtime testing document does not describe the stress action') | Out-Null
}

if ($errors.Count -gt 0) {
    Write-Output ('FAIL development-stress-contract errors={0}' -f $errors.Count)
    $errors | ForEach-Object { Write-Output ('FAIL development-stress-contract ' + $_) }
    exit 1
}
Write-Output 'PASS development-stress-contract bounded development-only action is documented'
exit 0
