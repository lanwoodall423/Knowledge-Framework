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
            if (stage == null) return new[] { "KnowledgeFramework_UnknownStage".Translate().ToString() };
            List<string> result = new List<string>();
            KnowledgeStageAggregate aggregate = KnowledgeStageAggregation.ForSubject(GameComponent_KnowledgeFramework.Current, schema,
                domainId, subjectId, pawn, scope, context, KnowledgeContextFallbackMode.ParentThenGlobal);
            if (aggregate.knowledge < stage.minimumKnowledge)
                result.Add("KnowledgeFramework_RequirementKnowledge".Translate(stage.minimumKnowledge.ToString("0.##")));
            if (aggregate.confidence < stage.minimumConfidence)
                result.Add("KnowledgeFramework_RequirementConfidence".Translate(stage.minimumConfidence.ToStringPercent()));
            if (stage.documented && !KnowledgeQuery.Subject(domainId, subjectId, pawn, scope).documented)
                result.Add("KnowledgeFramework_RequirementDocumentation".Translate());
            if (stage.requirementGroup != null && !KnowledgeRequirementService.Evaluate(stage.requirementGroup, domainId, subjectId, pawn, scope, context, out List<string> unmet))
                result.AddRange(unmet);
            return result.Distinct().ToList();
        }
    }
}
