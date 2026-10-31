# Static only: validates the 1.7 target contract without game binaries or execution.
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$failures = New-Object 'System.Collections.Generic.List[string]'
function Assert-Contract([bool]$condition, [string]$detail) {
    if (-not $condition) { $script:failures.Add($detail) | Out-Null }
}
function Read-Xml([string]$relative) {
    $path = Join-Path $root $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required XML file not found: $relative"
    }
    return [xml](Get-Content -LiteralPath $path -Raw)
}

try {
    $about = Read-Xml 'About/About.xml'
    $consumerAbout = Read-Xml 'Examples/ReferenceConsumer/About/About.xml'
    Assert-Contract (@($about.ModMetaData.supportedVersions.li).Count -eq 1 -and
        @($about.ModMetaData.supportedVersions.li)[0] -eq '1.7') 'framework About.xml must support 1.7 only'
    Assert-Contract (@($consumerAbout.ModMetaData.supportedVersions.li).Count -eq 1 -and
        @($consumerAbout.ModMetaData.supportedVersions.li)[0] -eq '1.7') 'consumer About.xml must support 1.7 only'
    Assert-Contract ($about.ModMetaData.packageId -eq 'lan.knowledgeframework') 'stable package ID changed'

    foreach ($prefix in @('', 'Examples/ReferenceConsumer/')) {
        $loader = Read-Xml ($prefix + 'LoadFolders.xml')
        $versions = @($loader.DocumentElement.ChildNodes | Where-Object { $_.NodeType -eq [Xml.XmlNodeType]::Element })
        Assert-Contract ($versions.Count -eq 1 -and $versions[0].Name -eq 'v1.7') ($prefix + 'LoadFolders must specify only v1.7')
        if ($versions.Count -eq 1) {
            $folders = @($versions[0].li | ForEach-Object { [string]$_ })
            Assert-Contract ($folders.Count -eq 1 -and $folders[0] -eq '1.7') ($prefix + 'LoadFolders must load 1.7 exactly once')
        }
    }

    foreach ($path in @(
        'Source/KnowledgeFramework.csproj',
        'DevTools/BridgeAdapter/KnowledgeFramework.BridgeAdapter.csproj',
        'DevTools/BehavioralHarness/KnowledgeFramework.BehavioralHarness.csproj',
        'Examples/ReferenceConsumer/ReferenceConsumer.csproj'
    )) {
        $project = Read-Xml $path
        $groups = @($project.Project.PropertyGroup | Where-Object { $_.TargetFramework })
        Assert-Contract ($groups.Count -eq 1 -and [string]$groups[0].TargetFramework -eq 'net10.0') ($path + ' must target net10.0')
        $source = Get-Content -LiteralPath (Join-Path $root $path) -Raw
        Assert-Contract ($source -notmatch '<Reference\s+Include="UnityEngine\.' -and $source -notmatch '1\.6/Assemblies') ($path + ' contains obsolete Unity or 1.6 reference')
        if ($path -notlike '*BehavioralHarness*') {
            $references = @($project.Project.ItemGroup.Reference | ForEach-Object { [string]$_.Include })
            foreach ($name in @('Assembly-CSharp', 'UnityStubs', 'Waylith')) {
                Assert-Contract ($references -contains $name) ($path + ' missing ' + $name + ' assembly reference')
            }
        }
    }

    foreach ($relative in @(
        '1.7/Defs/KnowledgeFramework_Defaults.xml',
        '1.7/Defs/MainButtonDefs.xml',
        '1.7/Languages/English/Keyed/KnowledgeFramework.xml',
        '1.7/Languages/English/DefInjected/MainButtonDef/KnowledgeFramework.xml',
        'Examples/ReferenceConsumer/1.7/Defs/ReferenceConsumer_Knowledge.xml',
        'Examples/ReferenceConsumer/1.7/Languages/English/Keyed/ReferenceConsumer.xml'
    )) {
        Read-Xml $relative | Out-Null
    }
    $tracked = @(& git -C $root ls-files)
    if ($LASTEXITCODE -ne 0) { throw 'git ls-files failed' }
    $obsolete = @($tracked | Where-Object { $_ -match '(^|/)1\.6/' })
    Assert-Contract ($obsolete.Count -eq 0) ('tracked 1.6 artifacts remain: ' + ($obsolete -join ', '))
    $stack = Get-Content -LiteralPath (Join-Path $root '.rimdev/stack.json') -Raw | ConvertFrom-Json
    Assert-Contract ($stack.deploymentTarget -eq '1.7/Assemblies/KnowledgeFramework.dll') 'RimTest stack deployment target is not 1.7'
    Assert-Contract (@($stack.runtimePackage.include) -contains '1.7/**') 'RimTest package must include 1.7 files'
    Assert-Contract (@($stack.runtimePackage.include) -notcontains '1.*/**') 'RimTest package wildcard must not include 1.6'
    $builder = Get-Content -LiteralPath (Join-Path $root 'DevTools/Build-KnowledgeFramework.ps1') -Raw
    foreach ($token in @('UnityStubs.dll', 'Waylith.dll', 'Harmony 3', "RimWorldTargetVersion -ne '1.7'")) {
        Assert-Contract ($builder.Contains($token)) ('Primary builder missing expected 1.7 guard: ' + $token)
    }

    $releaseDll = Join-Path $root '1.7/Assemblies/KnowledgeFramework.dll'
    $releaseManifest = Join-Path $root '1.7/Assemblies/KnowledgeFramework.build.json'
    Assert-Contract ((Test-Path -LiteralPath $releaseDll -PathType Leaf) -eq
        (Test-Path -LiteralPath $releaseManifest -PathType Leaf)) 'release DLL and manifest must appear as a pair'
}
catch { $failures.Add($_.Exception.Message) | Out-Null }

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) { Write-Output ('FAIL waylith17Contract ' + $failure) }
    Write-Output ('waylith17Contract=FAIL failures=' + $failures.Count)
    exit 1
}
Write-Output 'waylith17Contract=PASS static=1 runtime=NOT_RUN'
exit 0
