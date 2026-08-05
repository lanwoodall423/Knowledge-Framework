# Public API

The complete deterministic signature contract is in
`DevTools/PublicApiBaseline.txt`. It is the reviewable source of truth for
public API shape; this document defines the support classification and usage
boundaries.

## Stable

Stable APIs are intended for ordinary consumer mods and preserve source,
binary, Def/XML, and save compatibility within the documented generation:

- `KnowledgeFrameworkApi`
- `KnowledgeRegistry`, `KnowledgeDomainRegistry`, and domain/subject
  registration contracts
- `KnowledgeService`, `KnowledgeEngine`, `KnowledgeTransaction`,
  `KnowledgeTransactionResult`, `KnowledgeObservation`, and
  `KnowledgeMeasurement`
- `KnowledgeClaimValue`, claim/facet/subject/context snapshots, and
  `KnowledgeContextKey`
- `KnowledgeScope`, evidence/disposition enums, and stable result/value types
- `IKnowledgeContextPresentationProvider` and
  `IKnowledgeContextResolver`

Stable does not mean that a consumer may ignore the capability-generation
contract. Check `KnowledgeFrameworkApi.Supports(...)` before optional V2/V3
features.

## Advanced

Advanced APIs are supported integration surfaces requiring domain-specific
testing:

- `KnowledgeClaimService`, `KnowledgeMilestoneService`,
  `KnowledgeRelationService`, `KnowledgeDiscovery`, and `KnowledgeTransmission`
- contextual registration, presentation-provider, fallback, and query APIs
- expertise, accrual, comparison, effect, reveal, witness, and structured
  requirement APIs
- V3 definitions, snapshots, `GameComponent_KnowledgeFramework`, and UI
  provider interfaces

## Legacy

Legacy APIs remain available for existing consumer mods but should not be used
for new integrations:

- V1/V2 compatibility services and data contracts
- types with `V2` suffixes and the legacy migration/registration helpers
- members marked `[Obsolete]`

No obsolete member is removed in the `3.0.0-beta.1` release.

## Development/Diagnostic

These public types exist for diagnostics, testing, or framework-owned UI
integration and are not general gameplay contracts:

- `KnowledgeDiagnostics`, `KnowledgeValidation`,
  `KnowledgeFrameworkVerification`, and debug-action types
- `KnowledgeFrameworkStartup`, UI patch helpers, menu/debug windows, and
  verification result/diagnostic records

Consumers should not build gameplay correctness on these surfaces.

## Internal

Internal persistence records, index structures, merge helpers, cache keys,
Harmony details, and bridge implementation types are not public API and are
not compatibility promises. The public audit intentionally excludes types that
are not exported from the primary `KnowledgeFramework` namespace/assembly.

## Baseline Scope

The baseline records namespaces and type names, type kind/visibility,
static/abstract/sealed modifiers, base types, implemented interfaces, generic
parameters and constraints, constructors, methods and parameter modifiers,
defaults and return types, fields, properties and accessor visibility, events,
delegate signatures, enum names/numeric values, and relevant `[Obsolete]`
metadata. Methods-only interfaces and static service classes are included.
