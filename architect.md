# Shared Knowledge Framework Architecture

## Stable boundary

`KnowledgeFramework.dll` owns the four-rank contract, provider registry, reusable pawn/subject
record, cache, and the single pawn Bio presentation. It does not own domain progression data.
Wildlife and Aquaculture adapt their existing authoritative save records; Horticulture owns its
new additive pawn/crop records. Providers register by stable ID and replacement is idempotent.

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

The shared record serializes a pawn reference, subject def name, and XP, but adapters may retain
older records. No Wildlife or Aquaculture scribe key is renamed or duplicated. Missing providers,
pawns, or defs are ignored after load, and an old save without Horticulture knowledge initializes
an empty additive list.

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
- Existing scribe keys and progression effects are unchanged.
