# Building Knowledge Framework

Knowledge Framework does not download, commit, or redistribute RimWorld or
Harmony assemblies. A real framework build requires legitimate local copies
of those assemblies supplied by the developer or CI host.

## Primary build

PowerShell 7 is supported on Windows and Linux:

```powershell
pwsh ./DevTools/Build-KnowledgeFramework.ps1 `
  -ManagedPath /path/to/RimWorldWin64_Data/Managed `
  -HarmonyPath /path/to/0Harmony-directory `
  -RimWorldTargetVersion 1.6
```

On Windows PowerShell 5.1, use the same script with Windows paths:

```powershell
.\DevTools\Build-KnowledgeFramework.ps1 `
  -ManagedPath 'D:\Games\RimWorld\RimWorldWin64_Data\Managed' `
  -HarmonyPath 'D:\Games\RimWorld\Mods\Harmony\Assemblies'
```

The cross-platform shell entry point delegates to PowerShell 7:

```sh
./DevTools/build-knowledgeframework.sh \
  -ManagedPath /path/to/RimWorldWin64_Data/Managed \
  -HarmonyAssemblyPath /path/to/0Harmony.dll
```

The same locations may be supplied through `RIMWORLD_MANAGED_PATH` or
`RIMWORLD_MANAGED_ASSEMBLIES`, and `RIMWORLD_HARMONY_PATH` or
`RIMWORLD_HARMONY_ASSEMBLY`. Direct MSBuild uses
`KnowledgeFrameworkRimWorldManagedPath` and
`KnowledgeFrameworkHarmonyAssemblyPath`.

Release output is intentionally limited to:

- `1.6/Assemblies/KnowledgeFramework.dll`
- `1.6/Assemblies/KnowledgeFramework.build.json`

The manifest records the semantic version, assembly identity, DLL hash,
deterministic source-tree hash, UTC build time, and RimWorld target version.

## Bridge adapter

The optional adapter is built only when a local primary framework DLL and
local RimWorld assemblies are supplied:

```powershell
.\DevTools\Build-HotBridgeAdapter.ps1 `
  -ManagedPath 'D:\Games\RimWorld\RimWorldWin64_Data\Managed' `
  -PrimaryAssemblyPath '.\1.6\Assemblies\KnowledgeFramework.dll'
```

`KnowledgeFrameworkRimWorldManagedPath`/`RIMWORLD_MANAGED_PATH` and
`KnowledgeFrameworkPrimaryAssemblyPath`/`KNOWLEDGEFRAMEWORK_ASSEMBLY_PATH`
are also accepted by the adapter project. The final adapter DLL and manifest
are the only adapter artifacts intended for distribution; `bin`, `obj`, and
`BridgeAdapter/Build` are transient and ignored.

## Verification

Static checks that do not require RimWorld assemblies:

```powershell
pwsh ./DevTools/Verify-KnowledgeFramework.ps1 -AllowBlockedIntegration
```

Real-RimWorld checks after supplying local dependencies:

```powershell
pwsh ./DevTools/Verify-KnowledgeFramework.ps1 `
  -ManagedPath /path/to/RimWorldWin64_Data/Managed `
  -HarmonyPath /path/to/0Harmony-directory
pwsh ./DevTools/Run-KnowledgeFrameworkBehavioralTests.ps1 `
  -ManagedPath /path/to/RimWorldWin64_Data/Managed `
  -HarmonyPath /path/to/0Harmony-directory
```

The verification output labels each check `PASS`, `FAIL`, or
`BLOCKED/UNAVAILABLE`; blocked integration checks are not treated as static
contract failures when `-AllowBlockedIntegration` is used.
