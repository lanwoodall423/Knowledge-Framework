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
        public readonly int claimCount;
        public readonly int measurementCount;
        public readonly int contextCount;
        public readonly int milestoneCount;
        public readonly int relationCount;
        public readonly int accrualPolicyKeyCount;
        public readonly int subjectCacheInvalidations;
        public readonly long comparisonTicks;
        public readonly long migrationTicks;
        public readonly int witnessFanout;
        public readonly int recipeFanout;

        internal KnowledgeDiagnosticsSnapshot(long registrationTicks, long schemaBuildTicks, long transactionTicks,
            int transactionCount, int personal, int colony, int expertise, int orphans, long bytes,
            int hits, int misses, int insights, int relationships, int claims, int measurements, int contexts, int milestones,
            int relations, int accrual, int invalidations, long comparisons, long migrations, int witnessFanout, int recipeFanout)
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
            claimCount = claims;
            measurementCount = measurements;
            contextCount = contexts;
            milestoneCount = milestones;
            relationCount = relations;
            accrualPolicyKeyCount = accrual;
            subjectCacheInvalidations = invalidations;
            comparisonTicks = comparisons;
            migrationTicks = migrations;
            this.witnessFanout = witnessFanout;
            this.recipeFanout = recipeFanout;
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
        private static int subjectCacheInvalidations;
        private static long comparisonTicks;
        private static long migrationTicks;
        private static int witnessFanout;
        private static int recipeFanout;

        public static bool Enabled => Prefs.DevMode;

        public static KnowledgeDiagnosticsSnapshot Snapshot()
        {
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            return new KnowledgeDiagnosticsSnapshot(registrationTicks, schemaBuildTicks, transactionTicks, transactionCount,
                personalRecords, colonyRecords, expertiseRecords, orphanRecords, component?.ApproximatePersistentBytes() ?? 0L,
                cacheHits, cacheMisses, affectedInsights, affectedRelationships, component?.V3ClaimCount ?? 0,
                component?.V3MeasurementCount ?? 0, component?.V3ContextCount ?? 0, component?.V3MilestoneCount ?? 0,
                component?.V3RelationCount ?? 0, component?.V3AccrualCount ?? 0, subjectCacheInvalidations,
                comparisonTicks, migrationTicks, witnessFanout, recipeFanout);
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
        internal static void SubjectCacheInvalidated() { if (Enabled) subjectCacheInvalidations++; }
        internal static void RecordComparison(long ticks) { if (Enabled) comparisonTicks += ticks; }
        internal static void RecordMigration(long ticks) { if (Enabled) migrationTicks += ticks; }
        internal static void RecordFanout(int witnesses, int recipes)
        {
            if (!Enabled) return;
            witnessFanout += Math.Max(0, witnesses);
            recipeFanout += Math.Max(0, recipes);
        }
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
