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
$env:RIMWORLD_GAME_ROOT = 'D:\Games\RimWorld'
$env:RIMWORLD_HARMONY_PATH = 'D:\Games\RimWorld\Mods\Harmony\1.7\Assemblies'
dotnet run --project ./DevTools/BehavioralHarness/KnowledgeFramework.BehavioralHarness.csproj -c Release -- --game-root="$env:RIMWORLD_GAME_ROOT" --update-public-api-baseline="./DevTools/PublicApiBaseline.txt"
```

## Pull Requests

Describe behavior changes, compatibility impact, tests run, blocked runtime checks,
and any owner decision required. Keep generated intermediates out of changes. Use
the [release checklist](RELEASE_CHECKLIST.md) for release-oriented work.

The repository is licensed under the GNU General Public License, version 3 or
any later version. See [LICENSE](LICENSE) for the complete license text.
