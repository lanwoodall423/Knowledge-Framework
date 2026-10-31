# Building Knowledge Framework for RimWorld 1.7

**Only RimWorld 1.7 / Waylith is supported.** This branch is an unreleased migration until a real build and all validation gates pass. No 1.6 binaries are shipped or supported.

The game now uses **.NET 10**. The reference assemblies are in the **game installation root**, not the old Unity `RimWorldWin64_Data/Managed` directory:

- `Assembly-CSharp.dll`
- `UnityStubs.dll`
- `Waylith.dll`

Harmony 3 is a separate mod dependency. Use its 1.7 assembly at `<Harmony mod>/1.7/Assemblies/0Harmony.dll`. The build refuses Harmony 2. The repository never downloads, commits or distributes game/Harmony assemblies.

Prerequisites: a legitimately installed RimWorld 1.7 beta, Harmony 3, PowerShell 7 (or Windows PowerShell 5.1), and the .NET SDK specified by [global.json](global.json). Do not point to a 1.6 game installation.

## Build the framework

Example Windows PowerShell (edit both paths):

```powershell
.\DevTools\Build-KnowledgeFramework.ps1 `
  -GameRoot 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld' `
  -HarmonyAssemblyPath 'D:\Mods\Harmony\1.7\Assemblies\0Harmony.dll' `
  -RimWorldTargetVersion '1.7'
```

PowerShell 7 on macOS/Linux:

```powershell
pwsh ./DevTools/Build-KnowledgeFramework.ps1 `
  -GameRoot '/path/to/RimWorld' `
  -HarmonyAssemblyPath '/path/to/Harmony/1.7/Assemblies/0Harmony.dll'
```

`RIMWORLD_GAME_ROOT`, `RIMWORLD_HARMONY_PATH` (pointing at a mod root or assembly folder), and `RIMWORLD_HARMONY_ASSEMBLY` are accepted. The old `-ManagedPath` parameter name remains only as a tooling alias; its **value must still be the 1.7 game installation root**. The MSBuild property is `KnowledgeFrameworkRimWorldManagedPath` for compatibility with existing scripts, but it now means the game root. The `-RimWorldTargetVersion` argument rejects anything other than 1.7.

A successful primary build creates exactly:

- `1.7/Assemblies/KnowledgeFramework.dll`
- `1.7/Assemblies/KnowledgeFramework.build.json`

The manifest stores the source-tree SHA-256, the executable SHA-256, .NET SDK/compiler/MSBuild identities, version, reproducible build settings, 1.7 target, and file identities/hashes of Assembly-CSharp, UnityStubs, Waylith and Harmony 3. No proprietary reference DLL is copied into the mod package.

## Reference consumer and optional bridge adapter

After the framework builds:

```powershell
$env:RIMWORLD_GAME_ROOT = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld'
$env:KNOWLEDGEFRAMEWORK_ASSEMBLY_PATH = (Resolve-Path '.\1.7\Assemblies\KnowledgeFramework.dll').Path
dotnet build .\Examples\ReferenceConsumer\ReferenceConsumer.csproj -c Release
```

The optional DevBridge adapter needs the sibling `RimWorldDevBridge` publisher script and should be rebuilt under .NET 10:

```powershell
.\DevTools\Build-HotBridgeAdapter.ps1 `
  -GameRoot $env:RIMWORLD_GAME_ROOT `
  -PrimaryAssemblyPath $env:KNOWLEDGEFRAMEWORK_ASSEMBLY_PATH
```

Both adapters and consumers must use the same current 1.7 game references. Do not reuse the previously shipped 1.6 adapter DLL.

## Verification and release hold

Static checks are useful without game DLLs, but are **not** game/runtime proof:

```powershell
pwsh ./DevTools/Test-RimWorld17Contract.ps1
pwsh ./DevTools/Verify-KnowledgeFramework.ps1 -AllowBlockedIntegration
pwsh ./Examples/ReferenceConsumer/Verify-ReferenceConsumer.ps1
```

After supplying a local installation, rerun the full verification and behavioral harness:

```powershell
pwsh ./DevTools/Verify-KnowledgeFramework.ps1 -GameRoot $env:RIMWORLD_GAME_ROOT -HarmonyPath '/path/to/Harmony'
pwsh ./DevTools/Run-KnowledgeFrameworkBehavioralTests.ps1 -GameRoot $env:RIMWORLD_GAME_ROOT -HarmonyPath '/path/to/Harmony'
```

Then run RimTest's selected downstream and in-game suites, save/reload regression and Harmony 3 patch smoke described in [RUNTIME_TESTING.md](RUNTIME_TESTING.md) and [the 1.7 roadmap](ROADMAP_1_7_WAYLITH.md).

If no fresh 1.7 DLL, manifest or adapter exists, the check reports `BLOCKED/UNAVAILABLE`, not a release PASS. `-AllowBlockedIntegration` permits *only static work* to finish without treating these blockers as successes. The draft PR must not be merged or published on static checks alone.
