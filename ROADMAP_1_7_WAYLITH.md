# RimWorld 1.7 / Waylith migration roadmap

**Decision:** target RimWorld **1.7 only**. The 1.6 runtime and its distributions are retired; reading historical save records is still supported. **Status: draft migration; not release qualified.**

## Verified inputs (October 2026)

- Ludeon official 1.7 announcement: https://ludeon.com/blog/2026/10/announcing-the-waylith-engine-and-update-1-7/ . Waylith replaces Unity; 1.7 initially available on the Steam unstable beta channel; reported game performance improvements 2–4x, not measured for Knowledge Framework.
- Community developer migration guide: https://rimworldwiki.com/wiki/Modding_Tutorials/RimWorld_1.7_Mod_Updates . New `net10.0` target and game **installation-root** `Assembly-CSharp.dll`, `UnityStubs.dll` and `Waylith.dll`; Harmony 3 requires type-correct injections.
- Keep Unity namespace usages where backed by UnityStubs. Ludeon cautions against a wholesale move from IMGUI; inspect real API availability before adopting native Waylith rendering.

## P0 — 1.7-only source and package migration

- [x] Update primary/framework, reference consumer, bridge adapter, and harness project target to `net10.0` and replace obsolete Unity DLL references.
- [x] Mark both mods `1.7` only, with `LoadFolders.xml` loading version-specific content once.
- [x] Relocate Defs and language resources to `1.7/`; drop tracked legacy 1.6 DLL and obsolete bridge artifacts.
- [ ] Finish all build, provenance, release verification, RimTest and documentation paths so no 1.6 runtime target remains.
- [ ] Run compilation against licensed local **1.7 game-root binaries** and Harmony 3; regenerate signed-by-hash manifest and fresh `1.7/Assemblies/KnowledgeFramework.dll`.
- [ ] Review Harmony target signatures on real 1.7 assemblies; verify dependency order and new error handling.
- [ ] Refresh or deliberately accept public API baseline after comparing both consumer compatibility and saves; don't silently change the contract.

## P1 — Release qualification (mandatory)

- Static/packaging: all XML/JSON well-formed; 1.7-only folder; no stale binaries; semantic versions aligned; no transitive assembly copies.
- Build: `net10.0` primary, reference consumer, optional bridge; no hardcoded private binaries; deterministic release manifest with all reference assembly identities/hashes.
- In-game: framework alone, two independent consumers, new and old saves, schema registration, evidence/claims, transactions, migration idempotence, Bio panel, browser, diagnostic controls.
- Harmony 3: patch target existence and exact injection type checking; conflict detection against representative mod list.
- RimTest: smoke and release suites, save/reload, bounded stress before/after, impacted downstream test selection. Record precise PASS, FAIL or BLOCKED.
- Performance: idle TPS, query/transaction latency, indexed scans, UI frame latency, allocation count. Benchmark identical scenarios in Waylith, do not attribute game-wide gains to this mod.

## P2 — Engine-driven experiments (not release blockers)

| Opportunity | Experiment | Success criterion |
| --- | --- | --- |
| Frame Profiler (new built-in developer tool) | Profile knowledge UI vs no consumer and compare cold/warm cached queries | Documentable frame cost and fewer allocations |
| Fast mod startup and `-mod=` launcher | Launch base+Harmony+KnowledgeFramework+one consumer repeatedly with fixed seed | Deterministic smoke setup and shorter human test time |
| .NET 10 runtime | Prototype pooled or struct-based evidence/query intermediates behind existing public API | Better measured p95 time with unchanged serialization and API |
| Faster engine world ticks | Stress 1k subjects and 10k explicit observations | Event-driven architecture still beats polling; no new global tick hooks |
| Waylith-native graphics | Prototype **isolated** read-only knowledge graph view | Real documented API; improved frame cost/legibility; avoid replacing IMGUI prematurely |
| Threading opportunity | Compute pure immutable snapshots off-thread, never access Verse/game objects there | Repeatable throughput gain, no races in deterministic save/reload |

## P3 — Follow-up and risk management

- While 1.7 is unstable, validate each game beta update before release.
- Preserve `lan.knowledgeframework` package ID, stable domain/subject keys, historical save import, and consumer APIs. `1.7 only` does not mean deleting historical data migrations.
- Consumer release notes must explicitly call out the new minimum game version and .NET 10 runtime.
- Do not merge or publish a 1.7 build until P1 gates pass or each missing gate is explicitly recorded as a release blocker.

## Reference-compilation milestone (31 October 2026)

- [x] Add isolated .NET 10 reference-only CI compilation against Ludeon-permitted RimRef `1.7.4990-unstable` and Harmony `3.0.0-rc.4`. Initial framework and reference-consumer compilation PASSED on GitHub Actions; no game binaries were used or distributed.
- [x] Compile the optional bridge adapter and behavioral harness against the same reference baseline. All four components passed in GitHub Actions run 41254970698.
- [ ] Compile release assemblies against locally installed 1.7 game and Harmony 3, validate actual Harmony patches and execute in-game RimTest. **Reference-only compilation does not clear this gate.**
- CI only runs `push` checks on `main` and PR checks on other branches, avoiding duplicate Actions minutes.

## Test-candidate packaging

A short-lived CI artifact (if the PR job succeeds) contains a compiled 1.7-only **unverified test mod** with its Defs, localization and metadata. It contains none of the game/Harmony DLLs. This is an integration-testing convenience, **not** a Workshop release or a replacement for a game-root/Harmony 3 rebuild and RimTest qualification. Never use it as an established-good mod on a valuable save.
