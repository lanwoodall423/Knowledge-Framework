param(
    [string]$ManagedPath = $env:RIMWORLD_MANAGED_PATH,
    [string]$HarmonyPath = $env:RIMWORLD_HARMONY_PATH,
    [switch]$AllowBlockedIntegration
)

$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$failures = New-Object Collections.Generic.List[string]
$blocked = New-Object Collections.Generic.List[string]

function Pass-Check {
    param([string]$Name, [string]$Detail)
    Write-Output ('PASS {0}{1}' -f $Name, $(if ([string]::IsNullOrWhiteSpace($Detail)) { '' } else { ' ' + $Detail }))
}

function Fail-Check {
    param([string]$Name, [string]$Detail)
    $script:failures.Add($Name) | Out-Null
    Write-Output ('FAIL {0} {1}' -f $Name, $Detail)
}

function Block-Check {
    param([string]$Name, [string]$Detail)
    $script:blocked.Add($Name) | Out-Null
    Write-Output ('BLOCKED/UNAVAILABLE {0} {1}' -f $Name, $Detail)
}

function Invoke-LocalCheck {
    param([string]$Name, [scriptblock]$Action)
    try {
        & $Action
        Pass-Check -Name $Name -Detail 'completed'
    }
    catch {
        Fail-Check -Name $Name -Detail $_.Exception.Message
    }
}

$versionPath = Join-Path $root 'VERSION'
$version = $null
try {
    if (-not (Test-Path -LiteralPath $versionPath -PathType Leaf)) { throw "VERSION is missing: $versionPath" }
    $version = (Get-Content -LiteralPath $versionPath -Raw).Trim()
    if ($version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$') { throw "VERSION is not semantic: $version" }
    Pass-Check -Name 'version-format' -Detail $version
}
catch { Fail-Check -Name 'version-format' -Detail $_.Exception.Message }

$requiredFiles = @(
    'README.md',
    'About/About.xml',
    'LoadFolders.xml',
    '1.6/Assemblies/KnowledgeFramework.dll',
    '1.6/Assemblies/KnowledgeFramework.build.json',
    '1.6/Defs/KnowledgeFramework_Defaults.xml',
    '1.6/Defs/MainButtonDefs.xml',
    '1.6/Languages/English/Keyed/KnowledgeFramework.xml',
    '1.6/Languages/English/DefInjected/MainButtonDef/KnowledgeFramework.xml',
    'DevTools/PublicApiBaseline.txt',
    'API_COMPATIBILITY.md',
    'PUBLIC_API.md',
    'BUILDING.md',
    'INTEGRATION.md',
    'CHANGELOG.md',
    'CONTRIBUTING.md',
    'RELEASE_CHECKLIST.md',
    'WORKSHOP_RELEASE_NOTES_TEMPLATE.md',
    'RUNTIME_TESTING.md'
)
foreach ($relative in $requiredFiles) {
    if (Test-Path -LiteralPath (Join-Path $root $relative) -PathType Leaf) { Pass-Check -Name 'required-file' -Detail $relative }
    else { Fail-Check -Name 'required-file' -Detail "missing $relative" }
}

$xmlFiles = Get-ChildItem -LiteralPath $root -File -Recurse -Filter '*.xml' | Where-Object {
    $_.FullName -notmatch '[\\/]\.git[\\/]' -and $_.FullName -notmatch '[\\/]obj[\\/]'
}
$xmlInvalid = New-Object Collections.Generic.List[string]
foreach ($file in $xmlFiles) {
    try { [xml](Get-Content -LiteralPath $file.FullName -Raw) | Out-Null }
    catch { $xmlInvalid.Add($file.FullName) | Out-Null }
}
if ($xmlInvalid.Count -eq 0) { Pass-Check -Name 'xml-validity' -Detail ("{0} files" -f $xmlFiles.Count) }
else { Fail-Check -Name 'xml-validity' -Detail (($xmlInvalid -join ', ')) }

$keyedPath = Join-Path $root '1.6/Languages/English/Keyed/KnowledgeFramework.xml'
try {
    $keyedXml = [xml](Get-Content -LiteralPath $keyedPath -Raw)
    $definedKeys = New-Object Collections.Generic.HashSet[string]([StringComparer]::Ordinal)
    foreach ($node in $keyedXml.LanguageData.ChildNodes) {
        if ($node.NodeType -eq [Xml.XmlNodeType]::Element) { $definedKeys.Add($node.Name) | Out-Null }
    }
    $usedKeys = New-Object Collections.Generic.HashSet[string]([StringComparer]::Ordinal)
    foreach ($sourceFile in (Get-ChildItem -LiteralPath (Join-Path $root 'Source') -File -Recurse -Filter '*.cs')) {
        foreach ($match in [regex]::Matches((Get-Content -LiteralPath $sourceFile.FullName -Raw), '"(KnowledgeFramework_[A-Za-z0-9_]+)"\.Translate')) {
            $usedKeys.Add($match.Groups[1].Value) | Out-Null
        }
    }
    $missingKeys = @($usedKeys | Where-Object { -not $definedKeys.Contains($_) } | Sort-Object)
    if ($missingKeys.Count -eq 0) { Pass-Check -Name 'translation-key-coverage' -Detail ("{0} source keys" -f $usedKeys.Count) }
    else { Fail-Check -Name 'translation-key-coverage' -Detail ('missing ' + ($missingKeys -join ', ')) }
}
catch { Fail-Check -Name 'translation-key-coverage' -Detail $_.Exception.Message }

$baselinePath = Join-Path $root 'DevTools/PublicApiBaseline.txt'
try {
    $baselineLines = Get-Content -LiteralPath $baselinePath
    $declarations = @($baselineLines | Where-Object { $_ -match '^(TYPE|CTOR|METHOD|FIELD|PROPERTY|EVENT|ENUM|DELEGATE)\|' })
    $releaseLine = $baselineLines | Where-Object { $_.StartsWith('# release=', [StringComparison]::Ordinal) } | Select-Object -First 1
    if ($null -eq $releaseLine -or $releaseLine.Substring(10) -ne $version) { throw 'baseline release does not match VERSION' }
    if ($declarations.Count -eq 0) { throw 'baseline contains no declarations' }
    if (@($declarations | Sort-Object -Unique).Count -ne $declarations.Count) { throw 'baseline contains duplicate declarations' }
    for ($index = 1; $index -lt $declarations.Count; $index++) {
        if ([StringComparer]::Ordinal.Compare($declarations[$index - 1], $declarations[$index]) -gt 0) {
            throw 'baseline declarations are not deterministic ordinal order'
        }
    }
    Pass-Check -Name 'api-baseline' -Detail ("{0} declarations" -f $declarations.Count)
}
catch { Fail-Check -Name 'api-baseline' -Detail $_.Exception.Message }

Invoke-LocalCheck -Name 'release-contract' -Action { & (Join-Path $PSScriptRoot 'Test-ReleaseContract.ps1') }
Invoke-LocalCheck -Name 'bridge-artifact' -Action { & (Join-Path $PSScriptRoot 'Test-BridgeAdapter.ps1') }
Invoke-LocalCheck -Name 'build-manifest' -Action { & (Join-Path $PSScriptRoot 'New-KnowledgeFrameworkBuildManifest.ps1') -Verify }
Invoke-LocalCheck -Name 'documentation-links' -Action { & (Join-Path $PSScriptRoot 'Test-DocumentationLinks.ps1') }
Invoke-LocalCheck -Name 'development-stress-contract' -Action { & (Join-Path $PSScriptRoot 'Test-DevelopmentStressContract.ps1') }

$sourceProjects = Get-ChildItem -LiteralPath $root -File -Recurse -Filter '*.csproj' | Where-Object { $_.FullName -notmatch '[\\/]obj[\\/]' }
$absoluteHints = @($sourceProjects | ForEach-Object {
    Select-String -LiteralPath $_.FullName -Pattern '<HintPath>\s*(?:[A-Za-z]:[\\/]|/Users/|/home/|/mnt/)' -AllMatches
})
if ($absoluteHints.Count -eq 0) { Pass-Check -Name 'configurable-dependencies' -Detail 'no absolute HintPath values' }
else { Fail-Check -Name 'configurable-dependencies' -Detail (($absoluteHints | ForEach-Object { $_.Path + ':' + $_.LineNumber }) -join ', ') }

$assemblyOutput = Join-Path $root '1.6/Assemblies'
$allowedAssemblyFiles = @('KnowledgeFramework.dll', 'KnowledgeFramework.build.json')
$assemblyFiles = @(Get-ChildItem -LiteralPath $assemblyOutput -File)
$unexpectedAssemblyFiles = @($assemblyFiles | Where-Object { $allowedAssemblyFiles -notcontains $_.Name })
if ($unexpectedAssemblyFiles.Count -eq 0) { Pass-Check -Name 'release-output-layout' -Detail (($assemblyFiles | ForEach-Object Name) -join ', ') }
else { Fail-Check -Name 'release-output-layout' -Detail ('unexpected ' + (($unexpectedAssemblyFiles | ForEach-Object Name) -join ', ')) }

$trackedTransient = @(& git -C $root ls-files | Where-Object {
    $_ -match '(^|[\\/])(bin|obj|Build)([\\/]|$)' -or $_ -match '\.pdb$'
})
$packageTransient = New-Object Collections.Generic.List[string]
$zipFiles = @(Get-ChildItem -LiteralPath $root -File -Recurse -Filter '*.zip' | Where-Object {
    $_.FullName -notmatch '[\\/]\.git[\\/]'
})
if ($zipFiles.Count -gt 0) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    foreach ($zipFile in $zipFiles) {
        $archive = [IO.Compression.ZipFile]::OpenRead($zipFile.FullName)
        try {
            foreach ($entry in $archive.Entries) {
                if ($entry.FullName -match '(^|[\\/])(bin|obj|Build)([\\/]|$)' -or $entry.FullName -match '\.pdb$') {
                    $packageTransient.Add(($zipFile.Name + ':' + $entry.FullName)) | Out-Null
                }
            }
        }
        finally { $archive.Dispose() }
    }
}
if ($trackedTransient.Count -eq 0 -and $packageTransient.Count -eq 0) {
    $ignoredLocal = @(Get-ChildItem -LiteralPath $root -Directory -Recurse -ErrorAction SilentlyContinue | Where-Object {
        $_.FullName -notmatch '[\\/]\.git[\\/]' -and $_.Name -in @('bin', 'obj', 'Build')
    }).Count
    Pass-Check -Name 'transient-artifacts' -Detail ("tracked=0 packaged=0 ignored-local-directories={0}" -f $ignoredLocal)
}
else {
    $details = @($trackedTransient) + @($packageTransient)
    Fail-Check -Name 'transient-artifacts' -Detail ($details -join ', ')
}

if ([string]::IsNullOrWhiteSpace($ManagedPath)) { $ManagedPath = $env:RIMWORLD_MANAGED_ASSEMBLIES }
if ([string]::IsNullOrWhiteSpace($HarmonyPath)) { $HarmonyPath = $env:RIMWORLD_HARMONY_ASSEMBLIES }
if ([string]::IsNullOrWhiteSpace($ManagedPath) -or -not (Test-Path -LiteralPath $ManagedPath -PathType Container)) {
    Block-Check -Name 'real-RimWorld-build' -Detail 'supply -ManagedPath or RIMWORLD_MANAGED_PATH'
}
else {
    try {
        & (Join-Path $PSScriptRoot 'Build-KnowledgeFramework.ps1') -ManagedPath $ManagedPath -HarmonyPath $HarmonyPath
        if ($LASTEXITCODE -ne 0) { throw "build exited with code $LASTEXITCODE" }
        Pass-Check -Name 'real-RimWorld-build' -Detail 'completed with supplied local assemblies'
    }
    catch { Block-Check -Name 'real-RimWorld-build' -Detail $_.Exception.Message }
}

if ($failures.Count -gt 0) {
    Write-Output ('verification=FAIL failures={0} blocked={1}' -f $failures.Count, $blocked.Count)
    exit 1
}
if ($blocked.Count -gt 0 -and -not $AllowBlockedIntegration) {
    Write-Output ('verification=BLOCKED failures=0 blocked={0}; rerun with -AllowBlockedIntegration for static-only validation' -f $blocked.Count)
    exit 2
}
Write-Output ('verification=PASS failures=0 blocked={0}' -f $blocked.Count)
exit 0
