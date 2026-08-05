param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot '../1.6/Assemblies/KnowledgeFramework.dll'),
    [string]$ManifestDirectory = (Join-Path $PSScriptRoot 'BridgeAdapters')
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$versionPath = Join-Path $root 'VERSION'
$aboutPath = Join-Path $root 'About/About.xml'
$publicApiPath = Join-Path $root 'PUBLIC_API.md'
$compatibilityPath = Join-Path $root 'API_COMPATIBILITY.md'
$buildManifestPath = Join-Path $root '1.6/Assemblies/KnowledgeFramework.build.json'

if (-not (Test-Path -LiteralPath $versionPath -PathType Leaf)) { throw "VERSION is missing: $versionPath" }
$version = (Get-Content -LiteralPath $versionPath -Raw).Trim()
if ($version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$') { throw "VERSION is not semantic: $version" }

if (-not (Test-Path -LiteralPath $AssemblyPath -PathType Leaf)) { throw "Primary assembly is missing: $AssemblyPath" }
$assemblyInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo((Resolve-Path -LiteralPath $AssemblyPath).Path)
$assemblyName = [Reflection.AssemblyName]::GetAssemblyName((Resolve-Path -LiteralPath $AssemblyPath).Path)
$numericVersion = ($version -split '-', 2)[0] + '.0'
if ($assemblyName.Version.ToString() -ne $numericVersion) { throw "AssemblyVersion mismatch: $($assemblyName.Version) != $numericVersion" }
if ($assemblyInfo.FileVersion -ne $numericVersion) { throw "AssemblyFileVersion mismatch: $($assemblyInfo.FileVersion) != $numericVersion" }
if ($assemblyInfo.ProductVersion -ne $version) { throw "AssemblyInformationalVersion mismatch: $($assemblyInfo.ProductVersion) != $version" }
if ($assemblyInfo.FileDescription -ne 'Knowledge Framework') { throw "AssemblyTitle mismatch: $($assemblyInfo.FileDescription)" }
if ($assemblyInfo.ProductName -ne 'Knowledge Framework') { throw "Product metadata mismatch: $($assemblyInfo.ProductName)" }

$about = [xml](Get-Content -LiteralPath $aboutPath -Raw)
if ($about.ModMetaData.modVersion -ne $version) { throw "About.xml version mismatch: $($about.ModMetaData.modVersion) != $version" }

foreach ($documentation in @($publicApiPath, $compatibilityPath)) {
    if (-not (Test-Path -LiteralPath $documentation -PathType Leaf)) { throw "Documentation is missing: $documentation" }
    if ((Get-Content -LiteralPath $documentation -Raw).IndexOf($version, [StringComparison]::Ordinal) -lt 0) {
        throw "Documentation does not name release $version`: $documentation"
    }
}

$manifests = @(Get-ChildItem -LiteralPath $ManifestDirectory -File -Filter '*.manifest.json')
if ($manifests.Count -ne 1) { throw "Expected exactly one bridge manifest, found $($manifests.Count)." }
$manifest = Get-Content -LiteralPath $manifests[0].FullName -Raw | ConvertFrom-Json
if ($manifest.version -ne $version) { throw "Bridge manifest version mismatch: $($manifest.version) != $version" }

if (-not (Test-Path -LiteralPath $buildManifestPath -PathType Leaf)) { throw "Primary build manifest is missing: $buildManifestPath" }
$buildManifest = Get-Content -LiteralPath $buildManifestPath -Raw | ConvertFrom-Json
if ($buildManifest.semanticVersion -ne $version) { throw "Primary build manifest version mismatch: $($buildManifest.semanticVersion) != $version" }
if ($buildManifest.assemblyFile -ne [IO.Path]::GetFileName($AssemblyPath)) { throw "Primary build manifest assembly mismatch: $($buildManifest.assemblyFile)" }
if ($buildManifest.dllSha256 -ne (Get-FileHash -LiteralPath $AssemblyPath -Algorithm SHA256).Hash) { throw 'Primary build manifest hash does not match the DLL.' }

Write-Output "releaseContract=PASS version=$version assembly=$($assemblyName.Version) manifest=$($manifests[0].Name)"
