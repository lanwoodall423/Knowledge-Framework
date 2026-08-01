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

        internal KnowledgeRevealResult(bool revealed, float knowledge, float confidence, string stageId,
            string label, string description, bool approximate)
        {
            this.revealed = revealed;
            this.knowledge = knowledge;
            this.confidence = confidence;
            this.stageId = stageId;
            this.label = label;
            this.description = description;
            this.approximate = approximate;
        }
    }

    public static partial class KnowledgeDiscovery
    {
        public static KnowledgeStageSchema Stage(string domainId, string stageId) => KnowledgeRegistry.Schema(domainId)?.Stage(stageId);

        public static string CurrentStage(string domainId, string subjectId, Pawn pawn = null,
            KnowledgeScope scope = KnowledgeScope.Personal) => KnowledgeQuery.Subject(domainId, subjectId, pawn, scope).stageId;

        public static bool MeetsReveal(string domainId, string subjectId, string revealId, Pawn pawn = null,
            KnowledgeScope scope = KnowledgeScope.Personal)
        {
            KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId);
            KnowledgeRevealDef reveal = schema?.reveals.FirstOrDefault(item => item?.defName == revealId);
            KnowledgeScope revealScope = reveal?.colony == true ? KnowledgeScope.Colony : scope;
            KnowledgeFacetSnapshotV2 facet = KnowledgeQuery.Facet(domainId, subjectId, reveal?.facetId, pawn, revealScope);
            KnowledgeSubjectSnapshotV2 subject = KnowledgeQuery.Subject(domainId, subjectId, pawn, revealScope);
            if (reveal == null) return false;
            return facet.amount >= reveal.minimumKnowledge && facet.confidence >= reveal.minimumConfidence &&
                StageMet(schema, subject.stageId, reveal.minimumStageId);
        }

        public static KnowledgeRevealResult Present(string domainId, string subjectId, string facetId = null, Pawn pawn = null,
            KnowledgeScope scope = KnowledgeScope.Personal)
        {
            KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId);
            KnowledgeSubjectSnapshot subject = KnowledgeRegistry.ResolveSubject(domainId, subjectId);
            KnowledgeFacetSnapshotV2 value = KnowledgeQuery.Facet(domainId, subjectId, facetId, pawn, scope);
            KnowledgeFacetSchema facet = schema?.Facet(facetId);
            bool facetThresholdMet = facet == null || value.amount >= facet.revealKnowledge && value.confidence >= facet.revealConfidence;
            bool revealed = schema == null || facet?.hiddenUntilRevealed != true || facetThresholdMet;
            string label = revealed ? subject?.label : subject?.unidentifiedLabel;
            string description = revealed ? subject?.description : subject?.unidentifiedDescription;
            if (label.NullOrEmpty()) label = "KnowledgeFramework_Unidentified".Translate();
            if (description.NullOrEmpty()) description = "KnowledgeFramework_Unknown".Translate();
            bool approximate = !revealed || value.provisional || facet?.approximateWhenUncertain == true && value.confidence < 0.6f;
            return new KnowledgeRevealResult(revealed, value.amount, value.confidence,
                CurrentStage(domainId, subjectId, pawn, scope), label, description, approximate);
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
