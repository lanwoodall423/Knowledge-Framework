# Runtime Testing

Runtime checks require a legitimate local RimWorld installation and must be run in
a disposable test environment. The framework does not download, commit, or
redistribute proprietary assemblies. Static checks can run without them:

```powershell
pwsh ./DevTools/Verify-KnowledgeFramework.ps1 -AllowBlockedIntegration
pwsh ./DevTools/Test-DevelopmentStressContract.ps1
```

## New-Game Scenario

1. Start a new RimWorld 1.6 game with Harmony, Knowledge Framework, and one test
   consumer enabled.
2. Confirm the consumer domain, subjects, facets, stages, and localization appear.
3. Run the framework behavioral debug action and the bounded V3 stress validation
   action from the Development/Debug menu.
4. Record the stress report at `Config/KnowledgeFramework_Stress.txt`, including
   elapsed time, counts, cache statistics, and approximate bytes.

## Existing-Save Scenario

1. Load a save created before the consumer was installed.
2. Verify the framework loads with no consumer domain and does not alter unrelated
   player data.
3. Add the consumer, reload, and run its versioned migration.
4. Remove the consumer, save, reload, restore it, and verify the consumer's
   migration and aliases converge without duplicate histories.

## Reload and Multiple Consumers

Run the scenario with two consumers using different domain and source namespaces.
Save and reload between each registration. Verify cleanup of one consumer does not
remove the other's V2/V3 records, aliases, expertise, relations, or migrations.

## UI-Mod Compatibility

Repeat with the intended UI mods enabled. Verify browser filtering, context labels,
deep links, hidden-information rules, and cache invalidation after observations,
subject registration, and provider registration. Manual UI results must not be
reported as automated passes.

## Harmony Failure

Run once without Harmony or with an intentionally unavailable Harmony assembly.
The framework and static verification should report a clear blocked dependency
state without downloading files. Do not treat this as a successful runtime test.

## Exact Local Commands

Use only paths to assemblies already installed by the local RimWorld copy. These
commands build the framework and run the non-game behavioral checks; they do not
start RimWorld or copy proprietary assemblies into this repository.

Windows PowerShell:

```powershell
$managed = 'C:\Path\To\RimWorldWin64_Data\Managed'
$harmony = 'C:\Path\To\0Harmony.dll'
& .\DevTools\Build-KnowledgeFramework.ps1 -ManagedPath $managed -HarmonyAssemblyPath $harmony -RimWorldTargetVersion '1.6'
& .\DevTools\Run-KnowledgeFrameworkBehavioralTests.ps1 -SkipBuild -ManagedPath $managed -HarmonyPath $harmony -AssemblyPath (Resolve-Path '.\1.6\Assemblies\KnowledgeFramework.dll').Path
```

PowerShell 7 on Windows or Linux:

```powershell
$managed = '/path/to/RimWorldWin64_Data/Managed'
$harmony = '/path/to/0Harmony.dll'
pwsh ./DevTools/Build-KnowledgeFramework.ps1 -ManagedPath $managed -HarmonyAssemblyPath $harmony -RimWorldTargetVersion '1.6'
pwsh ./DevTools/Run-KnowledgeFrameworkBehavioralTests.ps1 -SkipBuild -ManagedPath $managed -HarmonyPath $harmony -AssemblyPath (Resolve-Path './1.6/Assemblies/KnowledgeFramework.dll').Path
```

The final stress step is deliberately in-game: start or load the disposable test
scenario, open the Development/Debug menu, and invoke `Run bounded V3 stress
validation`. If the map or pawn requirement is unavailable, record BLOCKED/
UNAVAILABLE and do not substitute a synthetic pass.

## Automatic In-Game Behavioral Verification

The mod-owned V2/V3 behavioral suite can run automatically in the disposable
DevBridge2 quicktest map. The wrapper below builds the mod, writes a one-shot
request marker, asks DevBridge2 to restart and wait for a playable map, then
waits for the report written by Knowledge Framework itself:

```powershell
& .\DevTools\Run-KnowledgeFrameworkInGameTests.ps1
```

DevBridge2 only coordinates the process, generation, readiness, and test lease;
all assertions execute in `KnowledgeFrameworkVerification.RunGameTests` inside
the mod assembly. The report is written to
`Config/KnowledgeFramework_AutomaticTest.txt`. The bounded stress scenario is
not folded into this suite because its required 20-live-pawn fixture must remain
an explicit BLOCKED/UNAVAILABLE result when that fixture is absent.

## Bounded Stress Action

The development-only action is implemented in
`Source/Development/KnowledgeFrameworkStressVerification.cs`. It is manually
invoked only while playing on a map, requires at least 20 live pawns, creates exactly
5,000 owned dynamic subjects, uses eight contexts and up to 20 witnesses, bounds
claim history/provenance/accrual state, exercises aliases, orphan detection,
consumer removal/restoration, dead/world-pawn queries, repeated rebuilds, browser
queries, long tick values, and persistence-size estimation. It writes only its own
report and cleans the owned stress domain in a `finally` block.

No tick polling is introduced. A missing map, insufficient pawns, or unavailable
local dependency is BLOCKED/UNAVAILABLE, not a pass.
