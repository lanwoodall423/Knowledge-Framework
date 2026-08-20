param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot '../1.6/Assemblies/KnowledgeFramework.dll'),
    [string]$ManifestPath = (Join-Path $PSScriptRoot '../1.6/Assemblies/KnowledgeFramework.build.json'),
    [string]$SourceRoot = (Join-Path $PSScriptRoot '..'),
    [string]$RimWorldTargetVersion = $env:RIMWORLD_TARGET_VERSION,
    [switch]$Verify
)

$ErrorActionPreference = 'Stop'

function Read-ReleaseVersion {
    param([string]$Root)
    $versionPath = Join-Path $Root 'VERSION'
    if (-not (Test-Path -LiteralPath $versionPath -PathType Leaf)) { throw "Release version file is missing: $versionPath" }
    $version = (Get-Content -LiteralPath $versionPath -Raw).Trim()
    if ($version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$') { throw "Release version is not semantic: $version" }
    return $version
}

function Get-SourceTreeHash {
    param([string]$Root)
    $rootFull = [IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $files = @(Get-ChildItem -LiteralPath $rootFull -File -Recurse | ForEach-Object {
        $relative = $_.FullName.Substring($rootFull.Length).TrimStart('\', '/').Replace('\', '/')
        $segments = $relative.Split('/')
        $excluded = $false
        foreach ($segment in $segments) {
            if ($segment -eq '.git' -or $segment -eq '.rimctx' -or $segment -eq 'bin' -or $segment -eq 'obj' -or $segment -eq 'Build') { $excluded = $true; break }
        }
        if ($relative.StartsWith('.rimdev/profiles/', [StringComparison]::OrdinalIgnoreCase) -or
            $relative.StartsWith('1.6/Assemblies/', [StringComparison]::OrdinalIgnoreCase) -or
            $relative.StartsWith('DevTools/BridgeAdapters/', [StringComparison]::OrdinalIgnoreCase) -or
            $_.Extension -in @('.pdb', '.dll', '.cache') -or
            $_.Name.EndsWith('.sourcelink.json', [StringComparison]::OrdinalIgnoreCase) -or
            $_.Name.EndsWith('.FileListAbsolute.txt', [StringComparison]::OrdinalIgnoreCase)) { $excluded = $true }
        if (-not $excluded) { [PSCustomObject]@{ Relative = $relative; Full = $_.FullName } }
    })

    # Sort-Object follows the current culture. Use an explicit ordinal insertion
    # sort so the manifest hash is identical on Windows and Linux.
    $sortedFiles = New-Object 'System.Collections.Generic.List[object]'
    foreach ($file in $files) {
        $insertAt = 0
        while ($insertAt -lt $sortedFiles.Count -and
            [StringComparer]::Ordinal.Compare([string]$sortedFiles[$insertAt].Relative, [string]$file.Relative) -lt 0) {
            $insertAt++
        }
        $sortedFiles.Insert($insertAt, $file)
    }

    $stream = New-Object IO.MemoryStream
    $utf8 = New-Object Text.UTF8Encoding($false)
    $textExtensions = @('.cs', '.csproj', '.props', '.targets', '.xml', '.json', '.md', '.ps1', '.txt', '.config', '.sln', '.yml', '.yaml', '.sh')
    $textFileNames = @('.gitignore', '.gitattributes', 'LICENSE', 'VERSION')
    foreach ($file in $sortedFiles) {
        $pathBytes = $utf8.GetBytes($file.Relative)
        $contentBytes = [IO.File]::ReadAllBytes($file.Full)
        if ($textExtensions -contains $([IO.Path]::GetExtension($file.Full).ToLowerInvariant()) -or
            $textFileNames -contains $file.Relative.Split('/')[-1]) {
            # Canonicalize text line endings so Windows and Linux checkout bytes produce the same release hash.
            $text = [Text.Encoding]::UTF8.GetString($contentBytes).Replace("`r`n", "`n").Replace("`r", "`n")
            $contentBytes = $utf8.GetBytes($text)
        }
        $stream.Write($pathBytes, 0, $pathBytes.Length)
        $stream.WriteByte(0)
        $stream.Write($contentBytes, 0, $contentBytes.Length)
        $stream.WriteByte(255)
    }
    try {
        $hash = [Security.Cryptography.SHA256]::Create().ComputeHash($stream.ToArray())
        return ([BitConverter]::ToString($hash).Replace('-', '')).ToUpperInvariant()
    }
    finally {
        $stream.Dispose()
    }
}

function Get-ManifestObject {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Build manifest is missing: $Path" }
    try { return (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json) }
    catch { throw "Build manifest is not valid JSON: $Path`n$($_.Exception.Message)" }
}

$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$assemblyFull = [IO.Path]::GetFullPath($AssemblyPath)
$manifestFull = [IO.Path]::GetFullPath($ManifestPath)
$sourceFull = [IO.Path]::GetFullPath($SourceRoot)
if ([string]::IsNullOrWhiteSpace($RimWorldTargetVersion)) { $RimWorldTargetVersion = '1.6' }
if (-not (Test-Path -LiteralPath $assemblyFull -PathType Leaf)) { throw "Primary assembly is missing: $assemblyFull" }

$version = Read-ReleaseVersion -Root $root
$assemblyName = [Reflection.AssemblyName]::GetAssemblyName($assemblyFull)
$fileInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($assemblyFull)
$dllHash = (Get-FileHash -LiteralPath $assemblyFull -Algorithm SHA256).Hash.ToUpperInvariant()
$sourceHash = Get-SourceTreeHash -Root $sourceFull

if ($Verify) {
    $manifest = Get-ManifestObject -Path $manifestFull
    $expectedAssemblyFile = [IO.Path]::GetFileName($assemblyFull)
    if ($manifest.manifestVersion -ne 1) { throw "Build manifest version is unsupported: $($manifest.manifestVersion)" }
    if ($manifest.semanticVersion -ne $version) { throw "Build manifest version mismatch: $($manifest.semanticVersion) != $version" }
    if ($manifest.assemblyFile -ne $expectedAssemblyFile) { throw "Build manifest assembly file mismatch: $($manifest.assemblyFile) != $expectedAssemblyFile" }
    if ($manifest.assemblyIdentity -ne $assemblyName.FullName) { throw 'Build manifest assembly identity does not match the DLL.' }
    if ($manifest.dllSha256 -ne $dllHash) { throw 'Build manifest DLL SHA-256 does not match the DLL.' }
    if ($manifest.sourceTreeSha256 -ne $sourceHash) { throw 'Build manifest source-tree SHA-256 does not match the current source tree.' }
    if ($manifest.rimWorldTargetVersion -ne $RimWorldTargetVersion) { throw "Build manifest RimWorld target mismatch: $($manifest.rimWorldTargetVersion) != $RimWorldTargetVersion" }
    try { [DateTimeOffset]::Parse($manifest.buildUtc, [Globalization.CultureInfo]::InvariantCulture) | Out-Null }
    catch { throw "Build manifest buildUtc is invalid: $($manifest.buildUtc)" }
    Write-Output ('buildManifest=PASS version={0} assembly={1} sha256={2} sourceTree={3} target={4}' -f $version, $assemblyName.FullName, $dllHash, $sourceHash, $RimWorldTargetVersion)
    exit 0
}

$manifestDirectory = Split-Path -Parent $manifestFull
if (-not (Test-Path -LiteralPath $manifestDirectory -PathType Container)) { New-Item -ItemType Directory -Force -Path $manifestDirectory | Out-Null }
$manifest = [ordered]@{
    manifestVersion = 1
    semanticVersion = $version
    assemblyFile = [IO.Path]::GetFileName($assemblyFull)
    assemblyIdentity = $assemblyName.FullName
    assemblyVersion = $assemblyName.Version.ToString()
    fileVersion = $fileInfo.FileVersion
    informationalVersion = $fileInfo.ProductVersion
    dllSha256 = $dllHash
    sourceTreeSha256 = $sourceHash
    buildUtc = [DateTimeOffset]::UtcNow.ToString('o', [Globalization.CultureInfo]::InvariantCulture)
    rimWorldTargetVersion = $RimWorldTargetVersion
}
$json = $manifest | ConvertTo-Json -Depth 4
[IO.File]::WriteAllText($manifestFull, $json + [Environment]::NewLine, (New-Object Text.UTF8Encoding($false)))
Write-Output ('buildManifest=CREATED path={0} version={1} sha256={2} sourceTree={3} target={4}' -f $manifestFull, $version, $dllHash, $sourceHash, $RimWorldTargetVersion)
