param(
    [string]$BridgeRoot = 'C:\Games\Steam\steamapps\common\RimWorld\Mods\RimWorldDevBridge'
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'BridgeAdapter\KnowledgeFramework.BridgeAdapter.csproj'
$build = Join-Path $PSScriptRoot 'BridgeAdapter\Build'
$destination = Join-Path $BridgeRoot 'DevTools\HotAdapters'
$publisher = Join-Path $BridgeRoot 'DevTools\Publish-RimWorldBridgeAdapter.ps1'
$source = Join-Path $PSScriptRoot 'BridgeAdapter\KnowledgeFrameworkBridgeAdapter.cs'
$stamp = Get-Date -Format 'yyyyMMddHHmmssfff'
$assemblyName = "KnowledgeFramework.BridgeAdapter.$stamp"

New-Item -ItemType Directory -Force -Path $build | Out-Null
dotnet build $project -c Release "-p:AssemblyName=$assemblyName" "-p:OutputPath=$build"
if ($LASTEXITCODE -ne 0) { throw "Knowledge Framework hot adapter build failed with exit code $LASTEXITCODE." }
if (-not (Test-Path -LiteralPath $publisher -PathType Leaf)) { throw "Bridge adapter publisher not found: $publisher" }

$built = Join-Path $build ($assemblyName + '.dll')
$text = [IO.File]::ReadAllText($source)
$specs = @([regex]::Matches($text, '"(?<spec>[A-Z][A-Z0-9_]*\|[RW]\|[^"\r\n]+)"') |
    ForEach-Object { $_.Groups['spec'].Value })
& $publisher -AssemblyPath $built -Destination $destination -AdapterId 'KnowledgeFramework' `
    -DisplayName 'Knowledge Framework' -Version '3.0.0' -Generation $stamp `
    -ProviderType 'KnowledgeFrameworkBridgeAdapter.KnowledgeFrameworkBridgeAdapter' -CommandSpecs $specs `
    -RequiredPackageIds @('lan.knowledgeframework') -NoMapCommands @('KF_V2_VALIDATE') `
    -TemporaryCommands @('KF_V2_VERIFY') -ExpensiveCommands @('KF_V2_VERIFY') -SimulationCommands @('KF_V2_VERIFY') `
    -ChangeSummary 'Bounded Knowledge Framework V2/V3 runtime verification.'
if ($LASTEXITCODE -ne 0) { throw "Knowledge Framework hot adapter publication failed with exit code $LASTEXITCODE." }
