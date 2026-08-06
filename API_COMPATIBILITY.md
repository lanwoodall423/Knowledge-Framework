# API Compatibility

## Release Identity

The current release is `3.1.0-beta.1`. `VERSION` is the authoritative semantic
release value. The primary `KnowledgeFramework` assembly uses it for
informational/file metadata, the bridge adapter and bridge manifest use it for
reported release identity, and the About/package metadata records the same
value.

`KnowledgeFrameworkApi.ApiVersion` is not a release number. It remains the
integer capability-generation contract and is currently `3`. Capability
generation and `CapabilityVersion(...)` values may remain stable across patch,
beta, and final releases.

This is a semver minor increment because it adds public consumer contracts without
removing or changing existing contracts. `ApiVersion` remains `3`; the new
consumer capabilities are generation-3 additions, not a semantic release number.

## Compatibility Guarantees

- **Source compatibility:** Existing public type names, member names, public
  fields, parameter types, defaults, and return types remain available in this
  release. Existing obsolete members are retained.
- **Binary compatibility:** Public removals, visibility reductions, field-type
  changes, method signature changes, return-type changes, enum-value changes,
  and incompatible base/interface changes are rejected by the public API
  baseline audit.
- **Def/XML compatibility:** Existing public Def/XML field names and types are
  retained. Obsolete members are not removed or repurposed in this release.
- **Save compatibility:** Existing V1/V2/V3 serialized records and migration
  paths remain supported. Internal implementation changes must not rename or
  reinterpret persisted fields without an explicit migration.
- **Capability compatibility:** Consumers should gate optional behavior with
  `KnowledgeFrameworkApi.ApiVersion`, `CapabilityVersion(...)`, and `Supports(...)`.
  Do not use the semantic release string as a capability-generation integer.
- **Deprecation compatibility:** `[Obsolete]` members remain callable. Their
  existing metadata is part of the audited public contract; deprecation does
  not imply removal in this release.

## Baseline Verification

`DevTools/PublicApiBaseline.txt` is a deterministic, human-readable baseline
for every public type in the primary assembly and its complete public/protected
surface: constructors, methods, fields, properties, events, delegates,
interfaces, generic constraints, defaults, enum values, and relevant obsolete
metadata.

The behavioral harness compares the source-built assembly by default. A
shipped DLL can be checked with:

```powershell
dotnet run --project DevTools\BehavioralHarness\KnowledgeFramework.BehavioralHarness.csproj -c Release --no-build -- `
  --managed="<RimWorld managed path>" `
  --assembly="<shipped>\KnowledgeFramework.dll"
```

An additive or incompatible change fails the audit. Updating the baseline is
an explicit owner action and must be reviewed with the corresponding public
API documentation change:

```powershell
dotnet run --project DevTools\BehavioralHarness\KnowledgeFramework.BehavioralHarness.csproj -c Release --no-build -- `
  --managed="<RimWorld managed path>" `
  --update-public-api-baseline="DevTools\PublicApiBaseline.txt"
```

The update command is not part of ordinary verification and does not silently
accept API changes.

## Consumer Guidance

Prefer stable entry points and capability checks. Treat Advanced APIs as
explicit integration surfaces, keep Legacy APIs for compatibility only, and
do not depend on Development/diagnostic or Internal implementation details.

### Consumer lifecycle boundary

Use `KnowledgeConsumerApi.Readiness` and call `PrepareRegistration()` before
registering runtime domains. Readiness becomes `Ready` after the current game's
framework component has completed initialization and framework-owned Def/schema
construction has succeeded. It resets to `Unavailable` when there is no current
game or framework state, and returns to `NotInitialized` during a game transition
until the new component finishes initialization. The framework schedules and owns
Def/schema construction; `PrepareRegistration()` is an idempotent preparation request, not
permission to call `KnowledgeRegistry.BuildDefSchemas()` directly. Registration is
supported only when the returned status is `Ready`. A no-game or missing-component
status is temporary unavailability; defer optional content and retry on the next
framework/game lifecycle rather than inspecting
`GameComponent_KnowledgeFramework.Current`.

`InitializationFailed` is terminal for the current framework/game lifecycle and
has the stable `SchemaBuildFailed` reason. Consumers should disable optional content
and report the failure; they must not retry by rebuilding global schemas.

`InspectDomainRegistration()` returns immutable owner, priority, and compatibility
metadata. `RegisterDomain()` accepts only an unregistered compatible domain and
always uses reject-on-conflict behavior, so a foreign owner is never replaced.

Use `InvalidateSubject`, bounded `InvalidateSubjects`, or explicit
`InvalidateDomain` for consumer-owned changes. These calls invalidate dynamic
subject snapshots and dependent V3 claim/stage/relation/comparison/UI
presentation caches through framework revisions. The bounded collection is capped
at `KnowledgeConsumerApi.MaxTargetedInvalidationSubjects` (256). Existing broad
`KnowledgeRegistry.InvalidateSubjects()` and `KnowledgeDomainRegistry.InvalidateDomain()`
remain available for compatibility.
