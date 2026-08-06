# Integration Guide

Knowledge Framework is an optional gameplay dependency. RimWorld Dev Bridge is an
optional development adapter, not a gameplay dependency. Consumers should reference
the public framework assembly and the `lan.knowledgeframework` package only.

## Register Content

Prefer static Defs for stable content. A domain may define facets, balanced stages,
expertise tracks, observations, typed claims, contexts, milestones, effects, and
relations. Give every consumer-owned Def, subject, facet, stage, claim, context,
milestone, and relation a stable ID. Localize the label and description keys in the
consumer's English keyed language file.

Dynamic subjects are appropriate for generated specimens or world content. Runtime
registration must happen after framework readiness:

```csharp
KnowledgeFrameworkReadinessStatus readiness = KnowledgeConsumerApi.PrepareRegistration();
if (!readiness.IsReady) return; // defer optional content; do not build schemas

KnowledgeConsumerRegistrationResult registration = KnowledgeConsumerApi.RegisterDomain(
    new KnowledgeDomainRegistration
    {
        id = "example.runtime-domain",
        label = "Example runtime domain",
        source = "example.mod"
    },
    new KnowledgeRegistrationOptions { source = "example.mod", priority = 100 });
if (!registration.Success) return; // a foreign owner is never replaced

KnowledgeRegistry.RegisterSubject("example.runtime-domain", new KnowledgeSubjectRegistration
{
    id = "specimen-001",
    label = "Specimen 001",
    source = "example.mod",
    applicableFacetIds = new[] { "identity", "habitat" },
    applicableClaimIds = new[] { "temperature" }
});
```

`KnowledgeConsumerApi.InspectDomainRegistration(...)` can be called before
registration to distinguish `Unregistered`, `RegisteredBySameOwner`,
`RegisteredByOtherOwner`, and `Incompatible`. It returns owner/source and priority
metadata only; never replace a foreign registration.

## Record An Observation

```csharp
KnowledgeTransactionResult result = KnowledgeEngine.Submit(new KnowledgeObservation
{
    observer = pawn,
    domainId = "example.domain",
    subjectId = "specimen-001",
    observationId = "field-observation",
    context = new KnowledgeContextKey("example.region", "north-field"),
    source = "example.mod",
    sourceInstanceId = "event-123"
});
```

Recipes can produce facets, typed `KnowledgeMeasurement` values, expertise,
documentation, shared expertise, witness learning, and bounded accrual state. A
direct measurement is useful when the consumer already has a typed value:

```csharp
new KnowledgeMeasurement
{
    domainId = "example.domain",
    subjectId = "specimen-001",
    facetId = "habitat",
    claimId = "temperature",
    value = KnowledgeClaimValue.Float(18.5f),
    observer = pawn,
    context = new KnowledgeContextKey("example.region", "north-field")
};
```

## Query and Share

Use `KnowledgeClaimService.Snapshot` for typed claims, `KnowledgeService` for
facets/stages/expertise, and `KnowledgeEffects.Query` for typed gameplay results.
`KnowledgeTransmission.Report` copies shareable personal evidence to colony
knowledge; `KnowledgeTransmission.Document` additionally records documentation.
Milestones and structural relations are opt-in and should be added only after the
consumer has verified the required definitions.

The minimal UI entry point is `KnowledgeV2Ui.Open(domainId, pawn, subjectId,
context)`. It safely does nothing when no game window stack is available. Context
presentation providers must authorize safe display values; stable IDs must not be
used as player-facing labels without explicit localization.

## Version and Capability Checks

```csharp
if (KnowledgeFrameworkApi.ApiVersion >= 3 &&
     KnowledgeFrameworkApi.Supports(3, KnowledgeFrameworkApi.TypedMeasurementsCapability) &&
     KnowledgeFrameworkApi.Supports(3, KnowledgeFrameworkApi.ReadinessInspectionCapability) &&
     KnowledgeFrameworkApi.Supports(3, KnowledgeFrameworkApi.RegistrationOwnershipCapability) &&
     KnowledgeFrameworkApi.Supports(3, KnowledgeFrameworkApi.TargetedInvalidationCapability) &&
     KnowledgeFrameworkApi.CapabilityVersion(KnowledgeFrameworkApi.ReadinessInspectionCapability) ==
         KnowledgeFrameworkApi.ThirdGenerationApiVersion)
{
    string release = KnowledgeFrameworkApi.ReleaseVersion;
}
```

Release version, integer API version, and capability-generation values are separate
contracts. Consumers should use capability checks for optional behavior and keep a
versioned `KnowledgeConsumerMigration` for persisted migrations.

## Targeted Invalidation

Invalidate after changing a consumer-owned subject or relationship instead of
touching framework components or global schema state:

```csharp
KnowledgeInvalidationResult result = KnowledgeConsumerApi.InvalidateSubject(
    "example.domain", "specimen-001");
if (!result.Success && result.code != KnowledgeInvalidationResultCode.SubjectNotFound)
    Log.Warning("Knowledge invalidation deferred: " + result.code);
```

Use `InvalidateSubjects(domainId, subjectIds)` for a bounded batch (maximum 256)
and `InvalidateDomain(domainId)` only when the whole domain changed. Both APIs
advance the framework revision and invalidate affected snapshots, claims, stages,
relations, comparisons, provider/UI caches, and context presentation snapshots.
The older broad invalidation methods remain compatibility APIs.

## Compatibility Rules

Keep consumer IDs stable across releases. Do not reinterpret old ownership or
accrual keys. Register aliases before content when a rename must be supported; the
framework migrates applicable V3 records immediately and remains safe when content
is temporarily absent. Existing V1/V2 saves and public obsolete members remain
supported according to [API_COMPATIBILITY.md](API_COMPATIBILITY.md).

The historical [INTEGRATION_V2.md](INTEGRATION_V2.md) filename remains as a
compatibility reference. This guide is the generation-neutral entry point.
