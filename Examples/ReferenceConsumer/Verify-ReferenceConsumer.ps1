param()

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$failures = New-Object Collections.Generic.List[string]

function Pass-Check([string]$name) { Write-Output ('PASS ' + $name) }
function Fail-Check([string]$name) { $failures.Add($name) | Out-Null; Write-Output ('FAIL ' + $name) }
function Require-File([string]$relative) {
    if (Test-Path -LiteralPath (Join-Path $root $relative) -PathType Leaf) { Pass-Check ('file ' + $relative) }
    else { Fail-Check ('missing file ' + $relative) }
}

foreach ($file in @('About/About.xml', 'LoadFolders.xml', 'ReferenceConsumer.csproj', 'ReferenceConsumerMod.cs', 'README.md',
        '1.6/Defs/ReferenceConsumer_Knowledge.xml', '1.6/Languages/English/Keyed/ReferenceConsumer.xml')) { Require-File $file }

foreach ($xml in @(Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.xml')) {
    try { [xml](Get-Content -LiteralPath $xml.FullName -Raw) | Out-Null; Pass-Check ('xml ' + $xml.Name) }
    catch { Fail-Check ('invalid xml ' + $xml.FullName) }
}

$about = [xml](Get-Content -LiteralPath (Join-Path $root 'About/About.xml') -Raw)
if ($about.ModMetaData.packageId -eq 'lan.knowledgeframework.referenceconsumer' -and
    $about.ModMetaData.dependencies.li.packageId -contains 'lan.knowledgeframework') { Pass-Check 'package identity and dependency' }
else { Fail-Check 'package identity and dependency' }

$frameworkRoot = [System.IO.Path]::GetFullPath((Join-Path $root '..\..'))
$frameworkLoadFolders = Join-Path $frameworkRoot 'LoadFolders.xml'
if ((Test-Path -LiteralPath $frameworkLoadFolders) -and
    (Get-Content -LiteralPath $frameworkLoadFolders -Raw) -notmatch 'Examples|ReferenceConsumer') { Pass-Check 'framework loader isolation' }
else { Fail-Check 'framework loader isolation' }

$defs = Get-Content -LiteralPath (Join-Path $root '1.6/Defs/ReferenceConsumer_Knowledge.xml') -Raw
$requiredIds = @('reference.field-guide', 'observation', 'habitat', 'temperature', 'fieldcraft', 'field-guide',
    'reference.region', 'reference.observed-from', 'reference-specimen')
foreach ($id in $requiredIds) {
    if ($defs.Contains($id)) { Pass-Check ('stable ID ' + $id) }
    else { Fail-Check ('stable ID ' + $id) }
}

$source = Get-Content -LiteralPath (Join-Path $root 'ReferenceConsumerMod.cs') -Raw
foreach ($api in @('KnowledgeEngine.Submit', 'KnowledgeTransmission.Report', 'KnowledgeTransmission.Document',
        'KnowledgeMilestoneService.Confirm', 'KnowledgeRelationService.Add', 'KnowledgeEffects.Query',
        'KnowledgeV2Ui.Open', 'KnowledgeMigrationService.Import', 'KnowledgeFrameworkApi.Supports')) {
    if ($source.Contains($api)) { Pass-Check ('API example ' + $api) }
    else { Fail-Check ('API example ' + $api) }
}

$translations = Get-Content -LiteralPath (Join-Path $root '1.6/Languages/English/Keyed/ReferenceConsumer.xml') -Raw
foreach ($key in @('ReferenceConsumer_DomainLabel', 'ReferenceConsumer_FacetObservation', 'ReferenceConsumer_FacetHabitat',
        'ReferenceConsumer_StageStudied', 'ReferenceConsumer_MilestoneDocumented', 'ReferenceConsumer_SubjectReference')) {
    if ($translations.Contains('<' + $key + '>')) { Pass-Check ('translation ' + $key) }
    else { Fail-Check ('translation ' + $key) }
}

foreach ($pattern in @('A:\\', '/Users/', '/home/', '<Reference Include="Harmony"', '0Harmony.dll', 'KnowledgeFramework.dll')) {
    if ($source.Contains($pattern) -or $defs.Contains($pattern) -or (Get-Content -LiteralPath (Join-Path $root 'ReferenceConsumer.csproj') -Raw).Contains($pattern)) {
        if ($pattern -eq 'KnowledgeFramework.dll') { continue }
        Fail-Check ('forbidden dependency/path ' + $pattern)
    }
}

$bundled = @(Get-ChildItem -LiteralPath $root -Recurse -File | Where-Object {
    $_.Extension -in @('.dll', '.pdb') -and $_.FullName -notmatch '[\\/](?:bin|obj|\.artifacts)[\\/]'
})
if ($bundled.Count -eq 0) { Pass-Check 'no bundled assemblies' } else { Fail-Check 'bundled assemblies' }

if ($failures.Count -eq 0) { Write-Output 'referenceConsumerVerification=PASS'; exit 0 }
Write-Output ('referenceConsumerVerification=FAIL failures=' + $failures.Count)
exit 1
