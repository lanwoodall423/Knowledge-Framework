# Knowledge Framework

Knowledge Framework is a RimWorld 1.6 gameplay dependency for mods that turn
observations into personal knowledge, confidence, discovery stages, typed claims,
expertise, documented colony knowledge, insights, and informed decisions. It is
event-driven and does not require consumer mods to poll ticks.

## Player Installation

Install the released Knowledge Framework folder as a normal RimWorld 1.6 mod and
place it before consumer mods that declare it as a dependency. Harmony is required
and is loaded before the framework. The framework is useful by itself: with no
consumer domains it registers no gameplay content, displays no consumer subjects,
and remains idle apart from its normal startup registration.

The framework does not include RimWorld, Unity, or Harmony assemblies. Those files
are supplied by the game and the Harmony mod.

## Mod-Author Dependency

Declare the dependency using the stable package ID and load order:

```xml
<li>
  <packageId>lan.knowledgeframework</packageId>
  <displayName>Knowledge Framework</displayName>
</li>
```

Use `lan.knowledgeframework` in `About/About.xml`; do not depend on a DLL filename
or a development checkout path. A small executable fixture is available in
[Examples/ReferenceConsumer](Examples/ReferenceConsumer/README.md).

## Five-Minute Integration

1. Add the package dependency and a Harmony dependency in your own `About.xml`.
2. Register a `KnowledgeDomainDef` with stable IDs for facets, stages, subjects,
   and any typed claims or observations.
3. Submit a `KnowledgeObservation` through `KnowledgeEngine` when gameplay shows
   the pawn learned something.
4. Query with `KnowledgeService`, `KnowledgeClaimService`, or the browser models.
5. Add optional milestones, relations, expertise, effects, transmission, and
   consumer migrations only when your content needs them.

See [INTEGRATION.md](INTEGRATION.md) for generation-neutral examples and
[BUILDING.md](BUILDING.md) for local dependency builds.

## Versions and Compatibility

The current semantic release is `3.1.0-beta.1`. The public integer API contract
remains `KnowledgeFrameworkApi.ApiVersion == 3`; capability generations are
reported separately from the release version. See
[API_COMPATIBILITY.md](API_COMPATIBILITY.md) and [PUBLIC_API.md](PUBLIC_API.md).

The supported game version is RimWorld `1.6`. Existing V1/V2 consumers and saved
data remain supported; V3 is additive. Obsolete public members are retained for
this release.

Runtime consumers should use `KnowledgeConsumerApi` for readiness, idempotent
registration preparation, ownership/conflict inspection, safe non-replacing domain
registration, and bounded subject invalidation. The framework owns schema
construction; consumers should not inspect `GameComponent_KnowledgeFramework.Current`
or call `KnowledgeRegistry.BuildDefSchemas()`.

## Troubleshooting

### The framework is not found

Confirm that the framework folder is enabled, its package ID is
`lan.knowledgeframework`, it is loaded before the consumer, and Harmony is active.

### A consumer has no subjects or stages

Check Def stable IDs, the consumer package dependency, and the consumer's load
order. A framework-only installation intentionally has no consumer domains.

### A local build reports missing assemblies

Supply a legitimate local RimWorld Managed directory and Harmony assembly. The
build scripts never download proprietary files; see [BUILDING.md](BUILDING.md).

### The API audit reports a change

Review the complete human-readable baseline in
[DevTools/PublicApiBaseline.txt](DevTools/PublicApiBaseline.txt). Only an
explicitly reviewed baseline update may accept an additive or incompatible API
change.

### A save was made without a consumer

This is safe. Framework records are namespaced by domain and consumer migrations
are versioned. When the consumer returns, it can register its Defs and run its
idempotent migration. See [RUNTIME_TESTING.md](RUNTIME_TESTING.md).

## Project Documents

- [Integration guide](INTEGRATION.md)
- [Compatibility contract](API_COMPATIBILITY.md)
- [Public API classifications](PUBLIC_API.md)
- [Build and verification](BUILDING.md)
- [Runtime testing](RUNTIME_TESTING.md)
- [Changelog](CHANGELOG.md)
- [Contributing](CONTRIBUTING.md)
- [Release checklist](RELEASE_CHECKLIST.md)
- [Workshop release-notes template](WORKSHOP_RELEASE_NOTES_TEMPLATE.md)
