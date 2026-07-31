using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace KnowledgeFramework
{
    public sealed class KnowledgeDiagnosticsSnapshot
    {
        public readonly long registrationTicks;
        public readonly long schemaBuildTicks;
        public readonly long transactionTicks;
        public readonly int transactionCount;
        public readonly int personalFacetRecords;
        public readonly int colonyFacetRecords;
        public readonly int expertiseRecords;
        public readonly int orphanRecords;
        public readonly long approximatePersistentBytes;
        public readonly int cacheHits;
        public readonly int cacheMisses;
        public readonly int affectedInsights;
        public readonly int affectedRelationships;

        internal KnowledgeDiagnosticsSnapshot(long registrationTicks, long schemaBuildTicks, long transactionTicks,
            int transactionCount, int personal, int colony, int expertise, int orphans, long bytes,
            int hits, int misses, int insights, int relationships)
        {
            this.registrationTicks = registrationTicks;
            this.schemaBuildTicks = schemaBuildTicks;
            this.transactionTicks = transactionTicks;
            this.transactionCount = transactionCount;
            personalFacetRecords = personal;
            colonyFacetRecords = colony;
            expertiseRecords = expertise;
            orphanRecords = orphans;
            approximatePersistentBytes = bytes;
            cacheHits = hits;
            cacheMisses = misses;
            affectedInsights = insights;
            affectedRelationships = relationships;
        }
    }

    public static class KnowledgeDiagnostics
    {
        private static long registrationTicks;
        private static long schemaBuildTicks;
        private static long transactionTicks;
        private static int transactionCount;
        private static int personalRecords;
        private static int colonyRecords;
        private static int expertiseRecords;
        private static int orphanRecords;
        private static int cacheHits;
        private static int cacheMisses;
        private static int affectedInsights;
        private static int affectedRelationships;

        public static bool Enabled => Prefs.DevMode;

        public static KnowledgeDiagnosticsSnapshot Snapshot()
        {
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            return new KnowledgeDiagnosticsSnapshot(registrationTicks, schemaBuildTicks, transactionTicks, transactionCount,
                personalRecords, colonyRecords, expertiseRecords, orphanRecords, component?.ApproximatePersistentBytes() ?? 0L,
                cacheHits, cacheMisses, affectedInsights, affectedRelationships);
        }

        internal static void RecordRegistration(long ticks) { if (Enabled) registrationTicks += ticks; }
        internal static void RecordSchemaBuild(long ticks, int domains, int subjects) { if (Enabled) schemaBuildTicks += ticks; }
        internal static void RecordTransaction(long ticks, int observations, int insights, int relationships)
        {
            if (!Enabled) return;
            transactionTicks += ticks;
            transactionCount++;
            affectedInsights += insights;
            affectedRelationships += relationships;
        }
        internal static void UpdateRecordCounts(int personal, int colony, int expertise, int orphans)
        {
            if (!Enabled) return;
            personalRecords = personal;
            colonyRecords = colony;
            expertiseRecords = expertise;
            orphanRecords = orphans;
        }
        internal static void CacheHit() { if (Enabled) cacheHits++; }
        internal static void CacheMiss() { if (Enabled) cacheMisses++; }
    }

    internal static class KnowledgeUiCache
    {
        private static int revision;
        internal static int Revision => revision;
        internal static void Invalidate(IEnumerable<KnowledgeChange> changes) { revision++; }
        internal static void Reset() { revision++; }
    }

    public static class KnowledgeValidation
    {
        public static IReadOnlyList<KnowledgeValidationIssue> ValidateAll()
        {
            List<KnowledgeValidationIssue> result = KnowledgeRegistry.ValidationIssues.ToList();
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            if (component != null && component.OrphanCount() > 0)
                result.Add(new KnowledgeValidationIssue("persistence.orphans", "save", component.OrphanCount() + " records reference unavailable domains or subjects.", false));
            return new ReadOnlyCollection<KnowledgeValidationIssue>(result);
        }
    }

    public static class KnowledgeFrameworkV2DebugActions
    {
        private const string Category = "Knowledge Framework";

        [DebugAction(Category, "Inspect version 2 schemas", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        private static void InspectSchemas()
        {
            List<string> lines = KnowledgeRegistry.SchemasSnapshot.Select(schema => schema.id + " facets=" + schema.facets.Count +
                " stages=" + schema.stages.Count + " tracks=" + schema.expertiseTracks.Count + " insights=" + schema.insights.Count +
                " relationships=" + schema.relationships.Count + " source=" + schema.source).ToList();
            Log.Message("[Knowledge Framework] V2 schemas\n" + (lines.Count == 0 ? "None" : string.Join("\n", lines)));
        }

        [DebugAction(Category, "Inspect version 2 selected pawn", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void InspectPawn()
        {
            Pawn pawn = Find.Selector.SingleSelectedThing as Pawn;
            if (pawn == null) { Messages.Message("KnowledgeFramework_SelectPawn".Translate(), MessageTypeDefOf.RejectInput, false); return; }
            List<string> lines = new List<string>();
            foreach (KnowledgeSchema schema in KnowledgeRegistry.SchemasSnapshot)
            {
                foreach (KnowledgeExpertiseTrackSchema track in schema.expertiseTracks)
                {
                    KnowledgeExpertiseSnapshotV2 expertise = KnowledgeQuery.Expertise(schema.id, pawn, track.id);
                    lines.Add(schema.id + "/" + track.id + " expertise=" + expertise.amount.ToString("0.##") + " " + expertise.rank);
                }
                lines.AddRange(KnowledgeQuery.PersonalFacets(schema.id, pawn).Select(item => item.subjectId + "/" + item.facetId +
                    " knowledge=" + item.amount.ToString("0.##") + " confidence=" + item.confidence.ToStringPercent() +
                    " evidence=" + item.evidenceCount + " revision=" + item.revision));
            }
            Log.Message("[Knowledge Framework] " + pawn.LabelShortCap + "\n" + string.Join("\n", lines));
        }

        [DebugAction(Category, "Inspect version 2 persistence", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        private static void InspectPersistence()
        {
            KnowledgeDiagnosticsSnapshot value = KnowledgeDiagnostics.Snapshot();
            Log.Message("[Knowledge Framework] persistence personal=" + value.personalFacetRecords + " colony=" + value.colonyFacetRecords +
                " expertise=" + value.expertiseRecords + " orphans=" + value.orphanRecords + " approximateBytes=" + value.approximatePersistentBytes +
                " cache=" + value.cacheHits + "/" + value.cacheMisses + " transactions=" + value.transactionCount);
        }

        [DebugAction(Category, "Validate version 2 framework", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.Playing)]
        private static void Validate()
        {
            IReadOnlyList<KnowledgeValidationIssue> issues = KnowledgeValidation.ValidateAll();
            Log.Message("[Knowledge Framework] V2 validation: " + (issues.Count == 0 ? "valid" : string.Join("\n", issues)));
        }
    }
}
