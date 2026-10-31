# RimWorld 1.7 / Waylith migration roadmap

Status: **in progress; not release-qualified**. Decision: **RimWorld 1.7 only**. No 1.6 runtime builds or releases. We retain serialized legacy knowledge data and public API compatibility where feasible, but not the old game runtime.

## Evidence and constraints

- Ludeon, 2026-10-12: Waylith replaces Unity; 1.7 is on Steam's unstable branch; large performance gains and most precompiled 1.6 mods remain compatible. https://ludeon.com/blog/2026/10/announcing-the-waylith-engine-and-update-1-7/
- Community migration guide (verify against the installed game): `net10.0`, `Assembly-CSharp.dll`, `UnityStubs.dll`, `Waylith.dll`, Harmony 3; remove direct UnityEngine module assembly references, but retain Unity namespaces where shim-supported. https://rimworldwiki.com/wiki/Modding_Tutorials/RimWorld_1.7_Mod_Updates
- Do **not** equate engine migration with automatic speedups in this framework. Measure a baseline first and avoid speculative direct Waylith API calls.
- The public package ID `lan.knowledgeframework` remains stable. No bundled or downloaded game/Harmony binaries.

## P0 — Make a clean 1.7-only build/release [required before release]

1. Convert all C# projects: primary, behavioral harness, bridge adapter and reference consumer to `net10.0`. Use the local **1.7** game and Harmony 3 references.
2. Eliminate direct `UnityEngine.*Module.dll` references in project/tooling and replace them with `UnityStubs.dll` and `Waylith.dll` where needed. Verify each assembly's real location using the installed game, rather than guessing paths.
3. Move packaged `1.6/Defs` and `1.6/Languages` to `1.7/`; move the reference-consumer data likewise. Emit a freshly compiled `1.7/Assemblies/KnowledgeFramework.dll` and build manifest; remove old bundled binaries and stale bridge artifacts.
4. Change `About/About.xml`, both `LoadFolders.xml` files, build scripts, manifest generator, output allowlists, `.rimdev/stack.json`, release contracts, CI, example paths and docs to 1.7 exclusively. Reject any 1.6 build target (fail closed).
5. Audit Harmony patches and load order under Harmony 3. Confirm each patch target and argument signature on actual 1.7 game assemblies; do not silence failures.
6. Update deterministic manifest: include 1.7 game assembly and Harmony 3 identities/hashes, SDK/toolchain version and the **new** 1.7-produced binary. Preserve release provenance and source hashes.
7. Preserve stable domain IDs, V1/V2/V3 public APIs and save migrations unless a compatibility break is explicitly documented.

## P1 — Validation gates [all required]

- Static: XML/JSON parse, 1.7-only packaging, no stray 1.6 paths or stale bundled 1.6 DLLs, config/version matching, documentation links, public API audit.
- Compile: .NET 10 release build with legitimate locally installed 1.7 and Harmony 3 assemblies; build the reference consumer and bridge adapter.
- Behavioral: pure verification; real in-game registration, Bio UI, context queries, transactions, evidence/claims, save/reload and old-save import on RimWorld 1.7.
- Integration: two independent consumer mods, load ordering, Harmony conflicts, DLC/no-DLC smoke, and RimTest-selected impacted consumer suites.
- Performance: measure idle overhead, cold load time, 10k observations, 1k subjects, query/cache throughput, UI frame cost and memory; compare measured scenarios, not theoretical engine claims.
- Track PASS / FAIL / BLOCKED separately. No Workshop release without in-game evidence.

## P2 — Waylith opportunities (experiments; not promises)

| Opportunity | Why useful | First experiment | Adoption gate |
|---|---|---|---|
| Faster large mod-load | Ludeon reports improved loading | 100+ synthetic knowledge Defs plus 2 consumers; compare cold starts | Repeatable improvement without more expensive schema build |
| Latest C# / .NET APIs | Modern runtime/toolchain | Prototype frozen record snapshots and allocation-efficient iterators | Same serialized contract, faster benchmark, no API break |
| Native Waylith UI/rendering | Engine may enable richer visualizations | Isolated read-only knowledge graph / provenance explorer prototype | Documented APIs, correct scaling and less frame cost; **keep Imgui/shim initially** |
| More budget for knowledge reasoning | Waylith raises general tick throughput | Compare bounded contextual query and evidence aggregation on 1.7 | No polling, no global scans, fixed time/memory ceiling |
| Improved instrumentation | Better profiling opportunities | Build per-domain counters: transaction, query, cache, schema, render | Development-only or opt-in; near-zero runtime overhead |
| Parallel/background computation | Potentially useful for immutable indexes | Prototype **off-main-thread pure snapshot computation only** | Demonstrated thread safety; never touch Verse/game objects off main thread |

## Explicitly out of scope

- Runtime support for RimWorld 1.6 or dual-target shipping.
- Replacing the Waylith compatibility shim or IMGUI prematurely.
- Claiming 1.7 runtime readiness before compiling and executing with real 1.7 binaries.
- Removing old save migrations solely because 1.6 runtime support ended.
