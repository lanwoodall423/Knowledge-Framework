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
            CurrentStageForContext(domainId, subjectId, pawn, scope, context, fallback);

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

        private static string CurrentStageForContext(string domainId, string subjectId, Pawn pawn, KnowledgeScope scope,
            KnowledgeContextKey context, KnowledgeContextFallbackMode fallback)
        {
            KnowledgeSubjectSnapshotV2 legacy = KnowledgeQuery.Subject(domainId, subjectId, pawn, scope);
            KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId);
            if (schema == null || context.IsPartial) return null;
            if (context.IsEmpty && schema.stageAggregationMode == KnowledgeStageAggregationMode.LegacySumMax) return legacy.stageId;
            KnowledgeStageAggregate aggregate = KnowledgeStageAggregation.ForSubject(GameComponent_KnowledgeFramework.Current, schema,
                domainId, subjectId, pawn, scope, context, fallback);
            string current = null;
            foreach (KnowledgeStageSchema stage in schema.stages.OrderBy(item => item.order))
            {
                if (aggregate.knowledge < stage.minimumKnowledge || aggregate.confidence < stage.minimumConfidence) continue;
                if (stage.documented && !legacy.documented) continue;
                if (stage.requirementGroup != null && !KnowledgeRequirementService.Evaluate(stage.requirementGroup, domainId,
                    subjectId, pawn, scope, context, out _)) continue;
                current = stage.id;
            }
            return current;
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
