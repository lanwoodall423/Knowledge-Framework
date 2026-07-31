# Knowledge Framework 2 Architecture

Knowledge Framework is a RimWorld 1.6 epistemic gameplay engine. Consumer mods own
domain balance and actions; the framework owns schemas, indexed state, observation
transactions, common queries, transmission contracts, persistence, diagnostics, and
shared presentation.

## Runtime layers

1. `KnowledgeDomainDef`, `KnowledgeFacetDef`, `KnowledgeStageDef`, expertise,
   observation, reveal, effect, insight, relationship, and transmission Defs describe
   static content. `KnowledgeRegistry.RegisterDomain` and `RegisterSubject` support
   dynamic content and generated subjects.
2. `KnowledgeSchema` is an immutable runtime schema built once after Def loading.
   `KnowledgeRegistry` validates stable IDs, thresholds, duplicate registrations,
   relationship cycles, and insight cycles. Dynamic subject resolvers are lazy and
   negatively cached with a bounded cache.
3. `KnowledgeEngine.Submit` is the mutation boundary. It validates every observation
   before applying any part of a batch, aggregates evidence, records bounded counters
   and optional provenance, updates personal or colony state, evaluates only touched
   insight dependencies, invalidates affected UI, and raises isolated change events.
4. `KnowledgeQuery` returns read-only snapshots for facet knowledge, confidence,
   evidence aggregates, provenance, familiarity, discovery, expertise, and derived
   relationship knowledge. Derived relationship values never recursively duplicate
   stored knowledge.
5. `GameComponent_KnowledgeFramework` stores separate personal subjects/facets,
   colony subjects/facets, expertise tracks, and insight activations. Runtime indexes
   use compact value keys; loaded duplicates are merged deterministically and the raw
   legacy records are removed only after successful idempotent import.

## Important semantics

- Knowledge is subject and facet understanding. Expertise is procedural competence and
  is never automatically copied from subject knowledge.
- Confidence is optional per domain. Supporting and contradictory evidence are stored
  independently; without uncertainty a domain can use the simple known/not-known view.
- Familiarity is practical exposure and is separate from knowledge completeness.
- Discovery stages are ordered by each domain. The framework does not require the
  four expertise ranks for knowledge stages.
- Colony knowledge is institutional state. It is not automatically a second personal
  XP bucket unless the domain chooses `Immediate` sharing.
- Failures can contribute evidence and expertise. Consumer balance is defined by the
  observation Def or direct observation values.
- Relationships are directional and bounded to `[0,1]`. Cycles are rejected during
  registration. Subject template transfer is a single derived hop.
- Typed effects have deterministic priority and explicit composition semantics. Legacy
  float effect providers remain supported through the V1 facade.

## Compatibility

The V1 symbols remain in `KnowledgeFramework.cs`, `KnowledgeDomains.cs`, and
`KnowledgeService.cs`: `KnowledgeRank`, `KnowledgeRecord`, provider registration,
`KnowledgeDomainDefinition`, `KnowledgeAward`, snapshots, `Award`, `ImportMinimum`,
reveal queries, float effects, and `KnowledgeMenuUI`. V1 awards are translated into
validated V2 observations. V1 domain registration creates a V2 domain with an
implicit facet and the default expertise track.

Existing saves retain the original keys while loading. Known V1 records are imported
by maximum value into V2, duplicate legacy records are merged, migration keys are
persisted, and migrated records are no longer written. Records for absent domains or
subjects remain orphaned and inert until the content returns. Subject and domain alias
registration supports intentional renames.

## Performance

There is no framework tick or map scan. Work occurs on registration, explicit
observations, transmission, queries, or visible UI. Evidence history is bounded by
domain schema limits. Insight and relationship dependencies are indexed. Bio entries,
dynamic subjects, schema lists, and UI models use revisions and bounded caches. The
development-only `KnowledgeDiagnostics.Snapshot()` reports registration/schema build
time, transaction time, record counts, orphan counts, cache hits, affected rules, and
an approximate persistence size.

## Presentation

The existing compact Bio panel remains the single Harmony extension point. V2 entries
are cached per pawn and capped to keep character cards usable. `KnowledgeMenuUI` is
the reusable legacy detailed renderer with safe GUI restoration and virtualized rows.
`KnowledgeV2Ui` and `Window_KnowledgeBrowser` provide a generic colonist/colony browser
with search, discovery state, facet completeness, confidence, provisional values,
and optional domain detail providers. Consumer mods can deep-link to a subject or
replace the domain detail panel without owning persistence.

See `INTEGRATION_V2.md` for the minimal integration path and advanced examples.
