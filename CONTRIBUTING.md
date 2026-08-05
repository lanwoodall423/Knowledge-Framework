# Contributing

Contributions should preserve the framework's optional-dependency behavior, stable
IDs, save compatibility, and no-tick-polling design. Do not add RimWorld or Harmony
assemblies to the repository.

## Local Checks

Run static checks without proprietary assemblies:

```powershell
pwsh ./DevTools/Verify-KnowledgeFramework.ps1 -AllowBlockedIntegration
pwsh ./Examples/ReferenceConsumer/Verify-ReferenceConsumer.ps1
git diff --check
```

With legitimate local game assemblies, use the commands in [BUILDING.md](BUILDING.md)
and run the behavioral harness. Runtime-only results must be reported as PASS,
FAIL, or BLOCKED/UNAVAILABLE; do not claim an unavailable game configuration passed.

## API Changes

Public API changes require an intentional review of
[PUBLIC_API.md](PUBLIC_API.md), the human-readable baseline, obsolete metadata, and
source/binary/save compatibility. Regenerate the baseline only after review:

```powershell
pwsh ./DevTools/Run-KnowledgeFrameworkBehavioralTests.ps1 -SkipBuild `
  -ManagedPath 'D:\Games\RimWorld\RimWorldWin64_Data\Managed' `
  -HarmonyPath 'D:\Games\RimWorld\Mods\Harmony\Assemblies' `
  -UpdatePublicApiBaseline
```

## Pull Requests

Describe behavior changes, compatibility impact, tests run, blocked runtime checks,
and any owner decision required. Keep generated intermediates out of changes. Use
the [release checklist](RELEASE_CHECKLIST.md) for release-oriented work.

The repository license is intentionally pending an owner decision. No `LICENSE`
file should be created until that decision is recorded.
