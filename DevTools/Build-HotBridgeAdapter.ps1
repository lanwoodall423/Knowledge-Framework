param(
    [string]$Destination = (Join-Path $PSScriptRoot 'BridgeAdapters'),
    [string]$PublisherPath = (Join-Path $PSScriptRoot '../../RimWorldDevBridge/DevTools/Publish-RimWorldBridgeAdapter.ps1'),
    [string]$ManagedPath = $env:RIMWORLD_MANAGED_PATH,
    [string]$PrimaryAssemblyPath = $env:KNOWLEDGEFRAMEWORK_ASSEMBLY_PATH
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'BridgeAdapter/KnowledgeFramework.BridgeAdapter.csproj'
$build = Join-Path $PSScriptRoot 'BridgeAdapter/Build'
$destination = [IO.Path]::GetFullPath($Destination)
$publisher = [IO.Path]::GetFullPath($PublisherPath)
$source = Join-Path $PSScriptRoot 'BridgeAdapter/KnowledgeFrameworkBridgeAdapter.cs'
$versionFile = Join-Path $PSScriptRoot '../VERSION'
$releaseVersion = (Get-Content -LiteralPath $versionFile -Raw).Trim()
if ([string]::IsNullOrWhiteSpace($releaseVersion)) { throw "Release version is empty: $versionFile" }
$stamp = Get-Date -Format 'yyyyMMddHHmmssfff'
$assemblyName = "KnowledgeFramework.BridgeAdapter.$stamp"

if ([string]::IsNullOrWhiteSpace($ManagedPath)) { $ManagedPath = $env:RIMWORLD_MANAGED_ASSEMBLIES }
if ([string]::IsNullOrWhiteSpace($PrimaryAssemblyPath)) { $PrimaryAssemblyPath = Join-Path $PSScriptRoot '../1.6/Assemblies/KnowledgeFramework.dll' }
if ([string]::IsNullOrWhiteSpace($ManagedPath) -or -not (Test-Path -LiteralPath $ManagedPath -PathType Container)) {
    $display = if ([string]::IsNullOrWhiteSpace($ManagedPath)) { '<missing>' } else { $ManagedPath }
    throw "adapterBuild=BLOCKED missing RimWorld Managed directory '$display'. Set -ManagedPath or RIMWORLD_MANAGED_PATH; proprietary assemblies are never downloaded."
}
foreach ($name in @('Assembly-CSharp.dll', 'UnityEngine.CoreModule.dll')) {
    $path = Join-Path $ManagedPath $name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "adapterBuild=BLOCKED missing required RimWorld assembly '$path'." }
}
if (-not (Test-Path -LiteralPath $PrimaryAssemblyPath -PathType Leaf)) { throw "adapterBuild=BLOCKED missing primary framework assembly '$PrimaryAssemblyPath'. Set -PrimaryAssemblyPath or KNOWLEDGEFRAMEWORK_ASSEMBLY_PATH." }

New-Item -ItemType Directory -Force -Path $build | Out-Null
if (Test-Path -LiteralPath $destination -PathType Container) {
    Get-ChildItem -LiteralPath $destination -File -Filter 'KnowledgeFramework.*.dll' |
        Remove-Item -Force
    Get-ChildItem -LiteralPath $destination -File -Filter 'KnowledgeFramework.*.manifest.json' |
        Remove-Item -Force
}
dotnet build $project -c Release "-p:AssemblyName=$assemblyName" "-p:OutputPath=$build" "-p:KnowledgeFrameworkRimWorldManagedPath=$ManagedPath" "-p:KnowledgeFrameworkPrimaryAssemblyPath=$PrimaryAssemblyPath"
if ($LASTEXITCODE -ne 0) { throw "Knowledge Framework hot adapter build failed with exit code $LASTEXITCODE." }
if (-not (Test-Path -LiteralPath $publisher -PathType Leaf)) { throw "Bridge adapter publisher not found: $publisher" }

$built = Join-Path $build ($assemblyName + '.dll')
$text = [IO.File]::ReadAllText($source)
$specs = @([regex]::Matches($text, '"(?<spec>[A-Z][A-Z0-9_]*\|[RW]\|[^"\r\n]+)"') |
    ForEach-Object { $_.Groups['spec'].Value })
& $publisher -AssemblyPath $built -Destination $destination -AdapterId 'KnowledgeFramework' `
    -DisplayName 'Knowledge Framework' -Version $releaseVersion -Generation $stamp `
    -ProviderType 'KnowledgeFrameworkBridgeAdapter.KnowledgeFrameworkBridgeAdapter' -CommandSpecs $specs `
    -RequiredPackageIds @('lan.knowledgeframework') -NoMapCommands @('KF_V2_VALIDATE') `
    -TemporaryCommands @('KF_V2_VERIFY') -ExpensiveCommands @('KF_V2_VERIFY') -SimulationCommands @('KF_V2_VERIFY') `
    -ChangeSummary 'Bounded Knowledge Framework V2/V3 runtime verification.'
if ($LASTEXITCODE -ne 0) { throw "Knowledge Framework hot adapter publication failed with exit code $LASTEXITCODE." }
