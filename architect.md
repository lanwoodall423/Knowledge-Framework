# Shared Knowledge Framework Architecture

## Stable boundary

`KnowledgeFramework.dll` owns the versioned domain API, subject registry, colony and pawn knowledge,
optional pawn expertise, reveal queries, effect and UI providers, change events, validation, caches,
save records, and the single pawn Bio presentation. Domain content and balance remain in consumers.
Legacy consumer stores can be imported idempotently or exposed through adapters; consumers never
receive mutable framework save records.

## Rank contract

All visible expertise uses Novice, Adept, Expert, and Master. A provider supplies current XP,
rank thresholds, a compact summary, a detailed tooltip, and an action that opens its existing
domain window. Ranks provide a normalized 0-3 bonus tier while each domain retains its own XP
scale and progression events.

## UI and performance

The framework patches the vanilla character card once. A collapsed `Knowledge & Expertise`
header expands to one compact row per registered active provider. Rows are cached per pawn for
60 ticks, use the vanilla UI palette plus rank-colored marks, expose complete tooltips, and
delegate clicks to existing mod windows. No adapter draws another Bio panel.

## Save compatibility

Framework records use stable domain and subject IDs and separate colony, pawn, and expertise lists.
Colony records contain no pawn reference. Horticulture reads its old `horticultureKnowledge` key and
imports personal values, aggregate colony values, expertise, and event counters by maximum-value
merge, then stops writing the obsolete key. Wildlife and Aquaculture retain their authoritative
legacy keys until their domain adapters are migrated. Missing domains and subjects remain as
diagnosable orphan records rather than being discarded.

## Standard detailed menu

`KnowledgeMenuUI` owns the shared Aquaculture-derived detailed presentation: adaptive two-pane
layout, Colonist/Colony navigation, alphabetized pawn selection, search, stable alphabetical subject
sorting, expertise progress, subject progress, tooltips, and empty states. Domain adapters supply
only immutable view models built from their authoritative records; the framework never persists or
mirrors progression data.

- Colonist mode reads one selected free colonist and preserves Bio-panel deep links.
- Colony mode uses the best available colonist expertise. Subject aggregation remains owned by the
  domain: Fish and Plant knowledge accumulate using their normalized or XP contracts, while
  Wildlife retains its established accumulated colony knowledge contract.
- Aquaculture keeps its Journal Expertise page, Horticulture keeps its Cultivar Registry Knowledge
  page, and Wildlife keeps its existing knowledge-window entry points.
- `KnowledgeMenuState` is presentation-only and resets safely when pawns leave the colony.
- Existing consumer scribe keys remain readable and progression effects are owned by domain effect
  providers rather than the framework.
