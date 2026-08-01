# Knowledge Framework V2 Integration

The framework is a normal gameplay dependency. RimWorld Dev Bridge is optional and
must not be added as a gameplay assembly reference.

V3 is an additive typed layer over the V1/V2 contracts. Consumers can adopt claims,
contexts, recipes, milestones, structural relations, and shared expertise per domain;
the existing V1 facade and V2 snapshots remain valid.

## Minimal domain

```csharp
KnowledgeRegistry.RegisterDomain(new KnowledgeDomainRegistration
{
    id = "example.domain",
    label = "Example knowledge",
    subjectResolver = id => new KnowledgeSubjectRegistration { id = id, label = id },
    subjectSource = () => subjects.Select(id => new KnowledgeSubjectRegistration { id = id, label = id }),
    source = "example.mod"
});
```

No facets means one implicit `default` facet. A minimal personal observation is:

```csharp
KnowledgeEngine.Submit(new KnowledgeObservation
{
    observer = pawn,
    domainId = "example.domain",
    subjectId = "subject.one",
    directKnowledge = 4f,
    directFamiliarity = 1f,
    reasonId = "studied",
    source = "example.mod"
});
```

## Facets and static Def content

Use `KnowledgeDomainDef` with referenced `KnowledgeFacetDef` values for identity,
habitat, diet, danger, or any other consumer-specific facts. `KnowledgeSubjectDef`
provides stable static subjects. Generated subjects are registered with
`KnowledgeRegistry.RegisterSubject` or returned by the domain's lazy resolver.

```csharp
KnowledgeRegistry.RegisterSubject("example.domain", new KnowledgeSubjectRegistration
{
    id = "generated.legendary-fish-17",
    label = "The Silver Current",
    templateSubjectId = "fish.template",
    templateKnowledgeCoefficient = 0.25f,
    source = "example.fishing"
});
```

## Sharing and documentation

```csharp
KnowledgeTransmission.Report("example.domain", subjectId, pawn, "field report");
KnowledgeTransmission.Document("example.domain", subjectId, pawn, "colony archive");
KnowledgeTransmission.Teach("example.domain", subjectId, teacher, student, 0.6f, "mentorship");
KnowledgeTransmission.ConsultArchive("example.domain", subjectId, student, 0.5f, "archive");
```

Choose `Immediate`, `Reportable`, `Documented`, or `Custom` in the domain schema.
The framework never forces a consumer to expose these actions.

## Queries and decisions

```csharp
KnowledgeFacetSnapshotV2 value = KnowledgeQuery.Facet(
    "example.domain", subjectId, "habitat", pawn);
KnowledgeRevealResult presentation = KnowledgeDiscovery.Present(
    "example.domain", subjectId, "habitat", pawn);
KnowledgeEffectResult result = KnowledgeEffects.Query(new KnowledgeEffectQuery
{
    domainId = "example.domain",
    subjectId = subjectId,
    channelId = "safe-to-harvest",
    pawn = pawn,
    basePermission = true
});
```

Use `result.permitted`, `result.revealed`, `result.actionIds`, or
`result.predictionAccuracy`; numeric modifiers are only one effect channel.

## Typed claims and contexts

Claims store typed measurements with bounded provenance and a configured aggregation
policy. Context is explicit and can fall back through a consumer-registered parent
chain.

```csharp
KnowledgeContextKey region = new KnowledgeContextKey("region", map.uniqueID.ToString());
KnowledgeEngine.Submit(new KnowledgeObservation
{
    observer = pawn,
    domainId = "example.domain",
    subjectId = subjectId,
    facetId = "habitat",
    context = region,
    claimMeasurements = new[]
    {
        new KnowledgeMeasurement
        {
            claimId = "temperature",
            value = KnowledgeClaimValue.Float(18f),
            source = "example.mod",
            summary = "field measurement"
        }
    }
});

KnowledgeClaimSnapshot claim = KnowledgeContextQuery.Claim(
    "example.domain", subjectId, "habitat", "temperature", region, pawn);
```

`KnowledgeObservationDef` can expand one observation into multiple facet outcomes,
claim measurements, expertise outcomes, witness distribution, and bounded accrual.
Use `KnowledgeSharedExpertiseService` for a namespace shared by multiple domains.

## Milestones, relations, and comparison

Milestones are sustained, ordered, and interruption-aware. Structural relations are
validated for parentage cycles and are retained even when referenced content is
temporarily absent.

```csharp
KnowledgeMilestoneService.Confirm(
    "example.domain", subjectId, "fieldwork", "established", pawn, region);

KnowledgeRelationService.Add(new KnowledgeSubjectRelation
{
    domainId = "example.domain",
    fromSubjectId = subjectId,
    toDomainId = "example.domain",
    toSubjectId = "subject.template",
    relationTypeId = "example.parent",
    confidence = 1f,
    context = region
});

KnowledgeComparisonSnapshot comparison = KnowledgeComparisonService.Compare(
    "example.domain", subjectId, "subject.template", pawn);
```

Use `KnowledgeTransmission.Transfer` when a consumer needs filtered facet/claim
transmission, confidence limits, contextual transfer, stage limits, or milestone
inclusion rather than one of the convenience methods.

## Insights and relationships

`KnowledgeInsightDef` requirements can use knowledge, confidence, event counts,
success/failure counts, familiarity, stages, expertise, related knowledge, custom
evaluators, or another insight. The framework evaluates an insight only when an
indexed dependency changes. Subscribe to `KnowledgeInsightService.InsightAvailable`
or `InsightConfirmed` for story/UI work.

`KnowledgeRelationshipDef` is directional. Define `fromSubjectId`, `toSubjectId`,
facet coefficients, and optional provisional confidence. Query derived values with
`KnowledgeQuery.Facet(...).derivedAmount` or `KnowledgeQuery.Relationships(...)`.

## Compatibility migration

Existing V1 consumers can keep using:

```csharp
KnowledgeDomainRegistry.RegisterDomain(oldDefinition);
KnowledgeService.Award(new KnowledgeAward { domainId = id, subjectId = subject,
    pawn = pawn, pawnKnowledge = 2f, colonyKnowledge = 1f, reasonId = "harvest" });
```

Legacy `KnowledgeRecord` stores should call `KnowledgeService.ImportMinimum`. The
operation is finite-value checked, idempotent, and maximum-merges progress and event
counts. Keep stable domain, subject, and reason IDs. For intentional renames, call
`KnowledgeRegistry.RegisterDomainAlias` or `RegisterSubjectAlias` before querying.

For a V3 migration, use `KnowledgeMigrationService.Import` with a stable
`consumerId` and monotonically increasing version. It can import subjects, claims,
milestones, and structural relations in one idempotent operation; check
`KnowledgeMigrationService.IsCommitted` before performing consumer-side cleanup.

## UI and diagnostics

Use `KnowledgeV2Ui.Open(domainId, pawn, subjectId)` for the generic browser, or
register `IKnowledgeDomainUiV2` for custom detail actions. Use `KnowledgeProviderRegistry`
only for compact Bio rows. Query snapshots are read-only and no persistence records
should be exposed to consumers.

Development mode exposes schema, pawn, persistence, validation, and verification
actions under the `Knowledge Framework` debug category. `KnowledgeDiagnostics.Snapshot`
is intentionally cheap and returns meaningful counters only in development mode.
Capability-aware consumers can inspect `KnowledgeFrameworkApi.ApiVersion` and
`KnowledgeFrameworkApi.CapabilityVersion(...)` before using optional V3 features.
