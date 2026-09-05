# Changelog

## 3.1.0-beta.2

- Added public-adoption documentation and a manually invoked bounded V3 stress
  validation action.
- Added runtime testing scenarios and release hygiene guidance.
- Completed a release-surface audit; no public API removals or visibility
  reductions are justified for this release candidate.

## 3.1.0-beta.1

- Added the stable `KnowledgeConsumerApi` readiness, idempotent preparation, safe
  non-replacing registration, ownership inspection, and bounded invalidation APIs.
- Added generation-3 capability constants for consumer migration, aliases,
  readiness, safe registration, ownership inspection, and targeted invalidation.
- Added behavioral coverage and a reference consumer using the supported lifecycle
  boundary without direct schema construction or destructive registration.
- Preserved all existing public members, V1/V2/V3 save migrations, Def/XML fields,
  and broad invalidation APIs.

## 3.0.0-beta.1

- Added the typed V3 integration layer for claims, contexts, balanced discovery,
  milestones, structural relations, shared expertise, typed effects, and filtered
  transmission.
- Added immediate, idempotent domain and subject alias migration across V3 state.
- Added bounded provider presentation and contextual stage provenance handling.
- Added a complete deterministic public API baseline and release contract checks.
- Added configurable local-dependency builds, build manifests, and an isolated
  reference consumer fixture.
- Retained obsolete public members and V1/V2 compatibility behavior.

## Unreleased

No unreleased changes.
