using System;
using System.Linq;
using Verse;

namespace KnowledgeFramework
{
    public sealed class KnowledgeRevealResult
    {
        public readonly bool revealed;
        public readonly float knowledge;
        public readonly float confidence;
        public readonly string stageId;
        public readonly string label;
        public readonly string description;
        public readonly bool approximate;
        public readonly bool identified;

        internal KnowledgeRevealResult(bool revealed, float knowledge, float confidence, string stageId,
            string label, string description, bool approximate, bool identified = false)
        {
            this.revealed = revealed;
            this.knowledge = knowledge;
            this.confidence = confidence;
            this.stageId = stageId;
            this.label = label;
            this.description = description;
            this.approximate = approximate;
            this.identified = identified;
        }
    }

    public static partial class KnowledgeDiscovery
    {
        public static KnowledgeStageSchema Stage(string domainId, string stageId) => KnowledgeRegistry.Schema(domainId)?.Stage(stageId);

        public static string CurrentStage(string domainId, string subjectId, Pawn pawn = null,
            KnowledgeScope scope = KnowledgeScope.Personal, KnowledgeContextKey context = default(KnowledgeContextKey),
            KnowledgeContextFallbackMode fallback = KnowledgeContextFallbackMode.ParentThenGlobal) =>
            StageSnapshot(domainId, subjectId, pawn, scope, context, fallback)?.stageId;

        public static KnowledgeStageSnapshot StageSnapshot(string domainId, string subjectId, Pawn pawn = null,
            KnowledgeScope scope = KnowledgeScope.Personal, KnowledgeContextKey context = default(KnowledgeContextKey),
            KnowledgeContextFallbackMode fallback = KnowledgeContextFallbackMode.ParentThenGlobal)
        {
            KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId);
            KnowledgeSubjectSnapshotV2 legacy = KnowledgeQuery.Subject(domainId, subjectId, pawn, scope);
            if (schema == null || context.IsPartial)
                return new KnowledgeStageSnapshot(domainId, subjectId, null, pawn, scope, context, KnowledgeContextKey.Empty, false, false);

            string persistedGlobalStage = legacy?.stageId;
            if (schema.Stage(persistedGlobalStage)?.contextSensitive == true) persistedGlobalStage = null;
            string globalStageId = context.IsEmpty && schema.stageAggregationMode == KnowledgeStageAggregationMode.LegacySumMax
                ? persistedGlobalStage ?? EvaluateStages(schema, domainId, subjectId, pawn, scope, KnowledgeContextKey.Empty,
                    KnowledgeContextFallbackMode.ExactOnly, legacy?.documented == true, false)
                : EvaluateStages(schema, domainId, subjectId, pawn, scope, KnowledgeContextKey.Empty,
                    KnowledgeContextFallbackMode.ExactOnly, legacy?.documented == true, false);
            string contextualStageId = null;
            KnowledgeContextKey resolvedContext = KnowledgeContextKey.Empty;
            bool usedFallback = false;
            if (schema.stages.Any(item => item.contextSensitive))
            {
                KnowledgeStageStateRecord persisted = FindContextualStage(domainId, subjectId, pawn, scope, context, fallback,
                    schema, out resolvedContext, out usedFallback);
                contextualStageId = persisted?.stageId;
                string calculated = EvaluateStages(schema, domainId, subjectId, pawn, scope, context, fallback,
                    legacy?.documented == true, true);
                if (IsLater(schema, calculated, contextualStageId)) contextualStageId = calculated;
                if (context.IsEmpty && contextualStageId.NullOrEmpty())
                    contextualStageId = calculated;
            }
            string selected = IsLater(schema, contextualStageId, globalStageId) ? contextualStageId : globalStageId;
            bool selectedContextual = !selected.NullOrEmpty() && schema.Stage(selected)?.contextSensitive == true;
            if (selectedContextual && resolvedContext.IsEmpty && !context.IsEmpty)
            {
                resolvedContext = context;
                usedFallback = false;
            }
            else if (!selectedContextual && !context.IsEmpty && !globalStageId.NullOrEmpty())
            {
                // A global stage is intentionally inherited by contextual views;
                // expose that fact instead of claiming exact-context progress.
                resolvedContext = KnowledgeContextKey.Empty;
                usedFallback = true;
            }
            return new KnowledgeStageSnapshot(domainId, subjectId, selected, pawn, scope, context,
                resolvedContext, usedFallback, selectedContextual);
        }

        public static bool MeetsReveal(string domainId, string subjectId, string revealId, Pawn pawn = null,
            KnowledgeScope scope = KnowledgeScope.Personal, KnowledgeContextKey context = default(KnowledgeContextKey),
            KnowledgeContextFallbackMode fallback = KnowledgeContextFallbackMode.ParentThenGlobal)
        {
            KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId);
            KnowledgeRevealDef reveal = schema?.reveals.FirstOrDefault(item => item?.defName == revealId);
            KnowledgeScope revealScope = reveal?.colony == true ? KnowledgeScope.Colony : scope;
            KnowledgeFacetSnapshotV2 facet = KnowledgeQuery.Facet(domainId, subjectId, reveal?.facetId, pawn, revealScope,
                true, true, context, fallback);
            KnowledgeSubjectSnapshotV2 subject = KnowledgeQuery.Subject(domainId, subjectId, pawn, revealScope);
            if (reveal == null) return false;
            return facet.amount >= reveal.minimumKnowledge && facet.confidence >= reveal.minimumConfidence &&
                StageMet(schema, CurrentStage(domainId, subjectId, pawn, revealScope, context, fallback), reveal.minimumStageId);
        }

        public static KnowledgeRevealResult Present(string domainId, string subjectId, string facetId = null, Pawn pawn = null,
            KnowledgeScope scope = KnowledgeScope.Personal, KnowledgeContextKey context = default(KnowledgeContextKey),
            KnowledgeContextFallbackMode fallback = KnowledgeContextFallbackMode.ParentThenGlobal)
        {
            KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId);
            KnowledgeSubjectSnapshot subject = KnowledgeRegistry.ResolveSubject(domainId, subjectId);
            KnowledgeFacetSnapshotV2 value = KnowledgeQuery.Facet(domainId, subjectId, facetId, pawn, scope, true, true, context, fallback);
            KnowledgeFacetSchema facet = schema?.Facet(facetId);
            string currentStage = CurrentStage(domainId, subjectId, pawn, scope, context, fallback);
            bool subjectIdentified = !currentStage.NullOrEmpty() || value.amount > 0f || value.evidenceCount > 0;
            bool facetThresholdMet = facet == null || value.amount >= facet.revealKnowledge && value.confidence >= facet.revealConfidence;
            bool facetRevealed = schema == null || facet?.hiddenUntilRevealed != true || facetThresholdMet;
            bool revealed = subjectIdentified && facetRevealed;
            string label = subjectIdentified ? subject?.label : subject?.unidentifiedLabel;
            string description = subjectIdentified ? subject?.description : subject?.unidentifiedDescription;
            if (label.NullOrEmpty()) label = "KnowledgeFramework_Unidentified".Translate();
            if (description.NullOrEmpty()) description = "KnowledgeFramework_Unknown".Translate();
            bool approximate = !revealed || value.provisional || facet?.approximateWhenUncertain == true && value.confidence < 0.6f;
            return new KnowledgeRevealResult(revealed, value.amount, value.confidence,
                currentStage, label, description, approximate, subjectIdentified);
        }

        internal static void RecordStageProgress(GameComponent_KnowledgeFramework component, KnowledgeSchema schema,
            string domainId, string subjectId, Pawn pawn, bool colony, KnowledgeSubjectStateRecord subject,
            KnowledgeContextKey context)
        {
            if (component == null || schema == null || subject == null || context.IsPartial) return;
            KnowledgeScope scope = colony ? KnowledgeScope.Colony : KnowledgeScope.Personal;
            string globalStage = EvaluateStages(schema, domainId, subjectId, pawn, scope, KnowledgeContextKey.Empty,
                KnowledgeContextFallbackMode.ExactOnly, subject.documented, false);
            KnowledgeStageSchema currentGlobal = schema.Stage(subject.stageId);
            if (currentGlobal?.contextSensitive == true) currentGlobal = null;
            KnowledgeStageSchema selectedGlobal = schema.Stage(globalStage);
            if (selectedGlobal != null && (currentGlobal == null || selectedGlobal.allowRegression || selectedGlobal.order >= currentGlobal.order))
                subject.stageId = selectedGlobal.id;
            else if (currentGlobal == null && subject.stageId != null)
                subject.stageId = schema.stages.Where(item => !item.contextSensitive).OrderBy(item => item.order)
                    .Select(item => item.id).FirstOrDefault();

            string contextualStage = EvaluateStages(schema, domainId, subjectId, pawn, scope, context,
                // Persist only evidence evaluated in the observation's exact
                // normalized context. Parent/global fallback is a query-time
                // view and must not materialize inherited progress into the
                // exact context record.
                KnowledgeContextFallbackMode.ExactOnly, subject.documented, true);
            if (contextualStage.NullOrEmpty()) return;
            KnowledgeStageStateRecord record = component.StageV3(domainId, subjectId, pawn, colony, context, true);
            KnowledgeStageSchema existing = schema.Stage(record.stageId);
            KnowledgeStageSchema selected = schema.Stage(contextualStage);
            if (selected != null && (existing == null || selected.allowRegression || selected.order >= existing.order)) record.stageId = selected.id;
            record.revision = Math.Max(0, record.revision + 1);
            record.lastTick = Find.TickManager?.TicksGame ?? 0;
            component.TouchV3();
        }

        private static string EvaluateStages(KnowledgeSchema schema, string domainId, string subjectId, Pawn pawn, KnowledgeScope scope,
            KnowledgeContextKey context, KnowledgeContextFallbackMode fallback, bool documented, bool contextualOnly)
        {
            if (schema == null) return null;
            KnowledgeStageAggregate aggregate = KnowledgeStageAggregation.ForSubject(GameComponent_KnowledgeFramework.Current, schema,
                domainId, subjectId, pawn, scope, context, fallback);
            string current = null;
            foreach (KnowledgeStageSchema stage in schema.stages.OrderBy(item => item.order))
            {
                if (stage.contextSensitive != contextualOnly) continue;
                if (aggregate.knowledge < stage.minimumKnowledge || aggregate.confidence < stage.minimumConfidence) continue;
                if (stage.documented && !documented) continue;
                KnowledgeContextKey requirementContext = stage.contextSensitive ? context : KnowledgeContextKey.Empty;
                if (stage.requirementGroup != null && !KnowledgeRequirementService.Evaluate(stage.requirementGroup, domainId,
                    subjectId, pawn, scope, requirementContext, out _)) continue;
                current = stage.id;
            }
            return current;
        }

        private static KnowledgeStageStateRecord FindContextualStage(string domainId, string subjectId, Pawn pawn,
            KnowledgeScope scope, KnowledgeContextKey context, KnowledgeContextFallbackMode fallback, KnowledgeSchema schema,
            out KnowledgeContextKey resolved, out bool usedFallback)
        {
            resolved = KnowledgeContextKey.Empty;
            usedFallback = false;
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            if (component == null) return null;
            bool colony = scope == KnowledgeScope.Colony;
            foreach (KnowledgeContextKey candidate in KnowledgeContextRegistry.Chain(context, fallback))
            {
                KnowledgeStageStateRecord record = component.StageV3(domainId, subjectId, colony ? null : pawn, colony, candidate, false);
                if (record == null || schema.Stage(record.stageId)?.contextSensitive != true) continue;
                resolved = candidate;
                usedFallback = !candidate.Equals(context);
                return record;
            }
            return null;
        }

        private static bool IsLater(KnowledgeSchema schema, string candidate, string current)
        {
            if (candidate.NullOrEmpty()) return false;
            if (current.NullOrEmpty()) return true;
            return (schema.Stage(candidate)?.order ?? int.MinValue) > (schema.Stage(current)?.order ?? int.MinValue);
        }

        private static bool StageMet(KnowledgeSchema schema, string actualId, string requiredId)
        {
            if (requiredId.NullOrEmpty()) return true;
            KnowledgeStageSchema actual = schema?.Stage(actualId);
            KnowledgeStageSchema required = schema?.Stage(requiredId);
            return actual != null && required != null && actual.order >= required.order;
        }
    }
}
