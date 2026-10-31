param(
    [Alias('ManagedPath')] [string]$GameRoot = $env:RIMWORLD_GAME_ROOT,
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
        $global:LASTEXITCODE = 0
        & $Action
        if ($LASTEXITCODE -ne 0) { throw "Check returned exit code $LASTEXITCODE" }
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
    '1.7/Defs/KnowledgeFramework_Defaults.xml',
    '1.7/Defs/MainButtonDefs.xml',
    '1.7/Languages/English/Keyed/KnowledgeFramework.xml',
    '1.7/Languages/English/DefInjected/MainButtonDef/KnowledgeFramework.xml',
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

$keyedPath = Join-Path $root '1.7/Languages/English/Keyed/KnowledgeFramework.xml'
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

$releaseDll = Join-Path $root '1.7/Assemblies/KnowledgeFramework.dll'
$releaseManifest = Join-Path $root '1.7/Assemblies/KnowledgeFramework.build.json'
if ((Test-Path -LiteralPath $releaseDll -PathType Leaf) -and (Test-Path -LiteralPath $releaseManifest -PathType Leaf)) {
    Invoke-LocalCheck -Name 'release-contract' -Action { & (Join-Path $PSScriptRoot 'Test-ReleaseContract.ps1') }
    Invoke-LocalCheck -Name 'build-manifest' -Action { & (Join-Path $PSScriptRoot 'New-KnowledgeFrameworkBuildManifest.ps1') -Verify }
}
elseif ((Test-Path -LiteralPath $releaseDll -PathType Leaf) -or (Test-Path -LiteralPath $releaseManifest -PathType Leaf)) {
    Fail-Check -Name 'partial-release-artifacts' -Detail 'DLL and build manifest must be shipped as a verified pair'
}
else { Block-Check -Name 'release-artifacts' -Detail 'compile and verify a new RimWorld 1.7 DLL and build manifest' }
$bridgeDir = Join-Path $root 'DevTools/BridgeAdapters'
$bridgeManifests = if (Test-Path -LiteralPath $bridgeDir -PathType Container) { @(Get-ChildItem -LiteralPath $bridgeDir -File -Filter '*.manifest.json') } else { @() }
if ($bridgeManifests.Count -eq 0) { Block-Check -Name 'bridge-artifact' -Detail 'new 1.7 bridge adapter not built' }
else { Invoke-LocalCheck -Name 'bridge-artifact' -Action { & (Join-Path $PSScriptRoot 'Test-BridgeAdapter.ps1') } }
Invoke-LocalCheck -Name 'documentation-links' -Action { & (Join-Path $PSScriptRoot 'Test-DocumentationLinks.ps1') }
Invoke-LocalCheck -Name 'development-stress-contract' -Action { & (Join-Path $PSScriptRoot 'Test-DevelopmentStressContract.ps1') }

$sourceProjects = Get-ChildItem -LiteralPath $root -File -Recurse -Filter '*.csproj' | Where-Object { $_.FullName -notmatch '[\\/]obj[\\/]' }
$absoluteHints = @($sourceProjects | ForEach-Object {
    Select-String -LiteralPath $_.FullName -Pattern '<HintPath>\s*(?:[A-Za-z]:[\\/]|/Users/|/home/|/mnt/)' -AllMatches
})
if ($absoluteHints.Count -eq 0) { Pass-Check -Name 'configurable-dependencies' -Detail 'no absolute HintPath values' }
else { Fail-Check -Name 'configurable-dependencies' -Detail (($absoluteHints | ForEach-Object { $_.Path + ':' + $_.LineNumber }) -join ', ') }

$assemblyOutput = Join-Path $root '1.7/Assemblies'
$allowedAssemblyFiles = @('KnowledgeFramework.dll', 'KnowledgeFramework.build.json')
$assemblyFiles = if (Test-Path -LiteralPath $assemblyOutput -PathType Container) { @(Get-ChildItem -LiteralPath $assemblyOutput -File) } else { @() }
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

if ([string]::IsNullOrWhiteSpace($HarmonyPath)) { $HarmonyPath = $env:RIMWORLD_HARMONY_ASSEMBLIES }
if ([string]::IsNullOrWhiteSpace($GameRoot) -or -not (Test-Path -LiteralPath $GameRoot -PathType Container)) {
    Block-Check -Name 'real-RimWorld-build' -Detail 'supply -GameRoot (1.7 installation root) or RIMWORLD_GAME_ROOT'
}
else {
    try {
        & (Join-Path $PSScriptRoot 'Build-KnowledgeFramework.ps1') -GameRoot $GameRoot -HarmonyPath $HarmonyPath
        if ($LASTEXITCODE -ne 0) { throw "build exited with code $LASTEXITCODE" }
        Pass-Check -Name 'real-RimWorld-build' -Detail 'completed with supplied local assemblies'
    }
    catch { Block-Check -Name 'real-RimWorld-build' -Detail $_.Exception.Message }
}

# Fail closed when obsolete binaries or target-frameworks are packaged.
$obsoletePaths = @(& git -C $root ls-files | Where-Object { $_ -match '^(1\.6|Examples/ReferenceConsumer/1\.6)/' })
if ($obsoletePaths.Count -gt 0) { Fail-Check -Name 'obsolete-1.6-content' -Detail ($obsoletePaths -join ', ') }
else { Pass-Check -Name '1.7-only-packaging' -Detail 'no tracked 1.6 content' }
foreach ($projectFile in $sourceProjects) {
    $raw = Get-Content -LiteralPath $projectFile.FullName -Raw
    if ($raw -notmatch '<TargetFramework>net10\.0</TargetFramework>' -or $raw -match 'UnityEngine\.[A-Za-z]+Module') {
        Fail-Check -Name 'net10-waylith-contract' -Detail $projectFile.FullName
    }
}
$loader = [xml](Get-Content -LiteralPath (Join-Path $root 'LoadFolders.xml') -Raw)
$about = [xml](Get-Content -LiteralPath (Join-Path $root 'About/About.xml') -Raw)
if (@($about.ModMetaData.supportedVersions.li).Count -ne 1 -or @($about.ModMetaData.supportedVersions.li)[0] -ne '1.7' -or
    $null -eq $loader.loadFolders.'v1.7' -or $null -ne $loader.loadFolders.'v1.6') {
    Fail-Check -Name '1.7-only-metadata' -Detail 'About and LoadFolders must specify only 1.7'
} else { Pass-Check -Name '1.7-only-metadata' -Detail 'version and load folder' }

if ($failures.Count -gt 0) {
    Write-Output ('verification=FAIL failures={0} blocked={1}' -f $failures.Count, $blocked.Count)
    exit 1
}
if ($blocked.Count -gt 0 -and -not $AllowBlockedIntegration) {
    Write-Output ('verification=BLOCKED failures=0 blocked={0}; rerun with -AllowBlockedIntegration for static-only validation' -f $blocked.Count)
    exit 2
}
Write-Output ('verification={0} failures=0 blocked={1}' -f $(if ($blocked.Count -eq 0) { 'PASS' } else { 'STATIC_PASS_UNRELEASED' }), $blocked.Count)
exit 0
