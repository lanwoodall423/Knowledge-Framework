using System.Collections.Generic;
using System.Linq;
using Verse;

namespace KnowledgeFramework
{
    public static partial class KnowledgeDiscovery
    {
        public static IReadOnlyList<string> UnmetStageRequirements(string domainId, string subjectId, string stageId,
            Pawn pawn = null, KnowledgeScope scope = KnowledgeScope.Personal, KnowledgeContextKey context = default(KnowledgeContextKey))
        {
            KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId);
            KnowledgeStageSchema stage = schema?.Stage(stageId);
            if (stage == null) return new[] { "Unknown stage." };
            List<string> result = new List<string>();
            KnowledgeFacetSnapshotV2 facet = KnowledgeQuery.Facet(domainId, subjectId, null, pawn, scope, true, false, context,
                KnowledgeContextFallbackMode.ParentThenGlobal);
            if (facet.amount < stage.minimumKnowledge) result.Add("Knowledge " + stage.minimumKnowledge.ToString("0.##"));
            if (facet.confidence < stage.minimumConfidence) result.Add("Confidence " + stage.minimumConfidence.ToStringPercent());
            if (stage.documented && !KnowledgeQuery.Subject(domainId, subjectId, pawn, scope).documented) result.Add("Documentation");
            if (stage.requirementGroup != null && !KnowledgeRequirementService.Evaluate(stage.requirementGroup, domainId, subjectId, pawn, scope, context, out List<string> unmet))
                result.AddRange(unmet);
            return result.Distinct().ToList();
        }
    }
}
