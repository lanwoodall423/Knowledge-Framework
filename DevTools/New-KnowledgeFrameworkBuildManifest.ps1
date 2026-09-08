param(
    [string]$AssemblyPath = (Join-Path $PSScriptRoot '../1.6/Assemblies/KnowledgeFramework.dll'),
    [string]$ManifestPath = (Join-Path $PSScriptRoot '../1.6/Assemblies/KnowledgeFramework.build.json'),
    [string]$SourceRoot = (Join-Path $PSScriptRoot '..'),
    [string]$RimWorldTargetVersion = $env:RIMWORLD_TARGET_VERSION,
    [string]$ManagedPath = $env:RIMWORLD_MANAGED_PATH,
    [string]$HarmonyAssemblyPath = $env:RIMWORLD_HARMONY_ASSEMBLY,
    [ValidateSet('Release')]
    [string]$Configuration = 'Release',
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
    function Select-SourceFile {
        param(
            [string]$Relative,
            [string]$Full
        )

        $fileInfo = Get-Item -LiteralPath $Full -Force
        $segments = $relative.Split('/')
        $excluded = $false
        foreach ($segment in $segments) {
            if ($segment -eq '.git' -or $segment -eq '.rimctx' -or $segment -eq 'bin' -or $segment -eq 'obj' -or $segment -eq 'Build') { $excluded = $true; break }
        }
        if ($relative.StartsWith('.rimdev/profiles/', [StringComparison]::OrdinalIgnoreCase) -or
            $relative.StartsWith('1.6/Assemblies/', [StringComparison]::OrdinalIgnoreCase) -or
            $relative.StartsWith('DevTools/BridgeAdapters/', [StringComparison]::OrdinalIgnoreCase) -or
            $fileInfo.Extension -in @('.pdb', '.dll', '.cache') -or
            $fileInfo.Name.EndsWith('.sourcelink.json', [StringComparison]::OrdinalIgnoreCase) -or
            $fileInfo.Name.EndsWith('.FileListAbsolute.txt', [StringComparison]::OrdinalIgnoreCase)) { $excluded = $true }
        if (-not $excluded) { [PSCustomObject]@{ Relative = $relative; Full = $fileInfo.FullName } }
    }

    # Prefer Git's tracked path set so ignored runtime/build state and provider-specific
    # hidden-file behavior cannot change the release hash.
    $gitPaths = @()
    $gitExitCode = 1
    $gitCommand = Get-Command git -ErrorAction SilentlyContinue
    if ($null -ne $gitCommand) {
        $gitPaths = @(& $gitCommand.Source -C $rootFull ls-files)
        $gitExitCode = $LASTEXITCODE
    }
    if ($gitExitCode -eq 0 -and $gitPaths.Count -gt 0) {
        $files = @($gitPaths | ForEach-Object {
            $relative = ([string]$_).Replace('\', '/')
            $full = Join-Path $rootFull ($relative.Replace('/', [IO.Path]::DirectorySeparatorChar))
            if (Test-Path -LiteralPath $full -PathType Leaf) { Select-SourceFile -Relative $relative -Full $full }
        })
    }
    else {
        $files = @(Get-ChildItem -LiteralPath $rootFull -Force -File -Recurse | ForEach-Object {
            $relative = $_.FullName.Substring($rootFull.Length).TrimStart('\', '/').Replace('\', '/')
            Select-SourceFile -Relative $relative -Full $_.FullName
        })
    }

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

function Get-ToolchainIdentity {
    $sdkVersion = (& dotnet --version).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sdkVersion)) { throw 'Unable to determine the selected .NET SDK version.' }
    $info = @(& dotnet --info)
    if ($LASTEXITCODE -ne 0) { throw 'Unable to determine .NET SDK information.' }
    $basePathLine = $info | Where-Object { $_ -match '^\s*Base Path:\s*(.+)$' } | Select-Object -First 1
    if ($null -eq $basePathLine) { throw 'Unable to determine the selected .NET SDK base path.' }
    $sdkBase = ([regex]::Match([string]$basePathLine, '^\s*Base Path:\s*(.+)$')).Groups[1].Value.Trim()
    $compilerPath = Join-Path $sdkBase 'Roslyn/bincore/csc.dll'
    if (-not (Test-Path -LiteralPath $compilerPath -PathType Leaf)) { throw "C# compiler is missing: $compilerPath" }
    $compilerVersion = [Reflection.AssemblyName]::GetAssemblyName($compilerPath).Version.ToString()
    $msbuildOutput = @(& dotnet msbuild -version -nologo)
    if ($LASTEXITCODE -ne 0) { throw 'Unable to determine the selected MSBuild version.' }
    $msbuildVersion = ($msbuildOutput | Where-Object { $_ -match '^\d+\.\d+(\.\d+)?' } | Select-Object -Last 1).Trim()
    if ([string]::IsNullOrWhiteSpace($msbuildVersion)) { throw 'Unable to parse the selected MSBuild version.' }
    [ordered]@{
        sdkVersion = $sdkVersion
        compilerVersion = $compilerVersion
        msbuildVersion = $msbuildVersion
    }
}

function Get-ExternalAssemblyInput {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "External build input is missing: $Path"
    }
    $assembly = [Reflection.AssemblyName]::GetAssemblyName($Path)
    [ordered]@{
        fileName = [IO.Path]::GetFileName($Path)
        assemblyIdentity = $assembly.FullName
        sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
    }
}

function Get-ExternalBuildInputs {
    param(
        [string]$Managed,
        [string]$Harmony
    )
    if ([string]::IsNullOrWhiteSpace($Managed) -or [string]::IsNullOrWhiteSpace($Harmony)) {
        return $null
    }
    [ordered]@{
        assemblyCSharp = Get-ExternalAssemblyInput -Path (Join-Path $Managed 'Assembly-CSharp.dll')
        unityCore = Get-ExternalAssemblyInput -Path (Join-Path $Managed 'UnityEngine.CoreModule.dll')
        unityGui = Get-ExternalAssemblyInput -Path (Join-Path $Managed 'UnityEngine.IMGUIModule.dll')
        unityText = Get-ExternalAssemblyInput -Path (Join-Path $Managed 'UnityEngine.TextRenderingModule.dll')
        harmony = Get-ExternalAssemblyInput -Path $Harmony
    }
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
$projectPath = Join-Path $root 'Source/KnowledgeFramework.csproj'
$projectXml = [xml](Get-Content -LiteralPath $projectPath -Raw)
$propertyGroup = @($projectXml.Project.PropertyGroup | Where-Object { $_.TargetFramework }) | Select-Object -First 1
$targetFramework = [string]$propertyGroup.TargetFramework
$languageVersion = [string]$propertyGroup.LangVersion
$deterministic = [string]$propertyGroup.Deterministic
$continuousIntegrationBuild = [string]$propertyGroup.ContinuousIntegrationBuild
$pathMap = [string]$propertyGroup.PathMap
$toolchain = Get-ToolchainIdentity
$externalInputs = Get-ExternalBuildInputs -Managed $ManagedPath -Harmony $HarmonyAssemblyPath

if ($Verify) {
    $manifest = Get-ManifestObject -Path $manifestFull
    $expectedAssemblyFile = [IO.Path]::GetFileName($assemblyFull)
    if ($manifest.manifestVersion -ne 1) { throw "Build manifest version is unsupported: $($manifest.manifestVersion)" }
    if ($manifest.semanticVersion -ne $version) { throw "Build manifest version mismatch: $($manifest.semanticVersion) != $version" }
    if ($manifest.assemblyFile -ne $expectedAssemblyFile) { throw "Build manifest assembly file mismatch: $($manifest.assemblyFile) != $expectedAssemblyFile" }
    if ($manifest.assemblyIdentity -ne $assemblyName.FullName) { throw 'Build manifest assembly identity does not match the DLL.' }
    if ($manifest.dllSha256 -ne $dllHash) { throw 'Build manifest DLL SHA-256 does not match the DLL.' }
    if ($manifest.sourceTreeSha256 -ne $sourceHash) {
        throw "Build manifest source-tree SHA-256 does not match the current source tree. manifest=$($manifest.sourceTreeSha256) computed=$sourceHash"
    }
    if ($manifest.configuration -ne $Configuration) { throw "Build manifest configuration mismatch: $($manifest.configuration) != $Configuration" }
    if ($manifest.targetFramework -ne $targetFramework) { throw "Build manifest target framework mismatch: $($manifest.targetFramework) != $targetFramework" }
    if ($manifest.sdkVersion -ne $toolchain.sdkVersion) { throw "Build manifest SDK mismatch: $($manifest.sdkVersion) != $($toolchain.sdkVersion)" }
    if ($manifest.compilerVersion -ne $toolchain.compilerVersion) { throw "Build manifest compiler mismatch: $($manifest.compilerVersion) != $($toolchain.compilerVersion)" }
    if ($manifest.msbuildVersion -ne $toolchain.msbuildVersion) { throw "Build manifest MSBuild mismatch: $($manifest.msbuildVersion) != $($toolchain.msbuildVersion)" }
    if ($manifest.languageVersion -ne $languageVersion -or
        $manifest.deterministic -ne $deterministic -or
        $manifest.continuousIntegrationBuild -ne $continuousIntegrationBuild -or
        $manifest.pathMap -ne $pathMap) {
        throw 'Build manifest deterministic compilation properties do not match the project.'
    }
    $inputNames = @('assemblyCSharp', 'unityCore', 'unityGui', 'unityText', 'harmony')
    foreach ($inputName in $inputNames) {
        $manifestProperty = $manifest.externalInputs.PSObject.Properties[$inputName]
        if ($null -eq $manifestProperty -or $null -eq $manifestProperty.Value) { throw "Build manifest external input is missing: $inputName" }
        if ($null -ne $externalInputs) {
            $currentInput = $externalInputs[$inputName]
            if ($manifestProperty.Value.sha256 -ne $currentInput.sha256) {
                throw "Build manifest external input hash mismatch: $inputName"
            }
            if ($manifestProperty.Value.assemblyIdentity -ne $currentInput.assemblyIdentity) {
                throw "Build manifest external input identity mismatch: $inputName"
            }
        }
    }
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
    buildContractVersion = 1
    semanticVersion = $version
    assemblyFile = [IO.Path]::GetFileName($assemblyFull)
    assemblyIdentity = $assemblyName.FullName
    assemblyVersion = $assemblyName.Version.ToString()
    fileVersion = $fileInfo.FileVersion
    informationalVersion = $fileInfo.ProductVersion
    dllSha256 = $dllHash
    sourceTreeSha256 = $sourceHash
    configuration = $Configuration
    targetFramework = $targetFramework
    sdkVersion = $toolchain.sdkVersion
    compilerVersion = $toolchain.compilerVersion
    msbuildVersion = $toolchain.msbuildVersion
    languageVersion = $languageVersion
    deterministic = $deterministic
    continuousIntegrationBuild = $continuousIntegrationBuild
    pathMap = $pathMap
    externalInputs = $externalInputs
    buildUtc = [DateTimeOffset]::UtcNow.ToString('o', [Globalization.CultureInfo]::InvariantCulture)
    rimWorldTargetVersion = $RimWorldTargetVersion
}
$json = $manifest | ConvertTo-Json -Depth 4
[IO.File]::WriteAllText($manifestFull, $json + [Environment]::NewLine, (New-Object Text.UTF8Encoding($false)))
Write-Output ('buildManifest=CREATED path={0} version={1} sha256={2} sourceTree={3} target={4}' -f $manifestFull, $version, $dllHash, $sourceHash, $RimWorldTargetVersion)
