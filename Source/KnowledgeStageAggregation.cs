using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace KnowledgeFramework
{
    internal struct KnowledgeStageFacetValue
    {
        public readonly float amount;
        public readonly float completenessAmount;
        public readonly float confidence;
        public readonly int evidenceCount;

        internal KnowledgeStageFacetValue(float amount, float completenessAmount, float confidence, int evidenceCount)
        {
            this.amount = amount;
            this.completenessAmount = completenessAmount;
            this.confidence = confidence;
            this.evidenceCount = evidenceCount;
        }
    }

    internal struct KnowledgeStageAggregate
    {
        public readonly float knowledge;
        public readonly float confidence;

        internal KnowledgeStageAggregate(float knowledge, float confidence)
        {
            this.knowledge = knowledge;
            this.confidence = confidence;
        }
    }

    internal static class KnowledgeStageAggregation
    {
        /// <summary>
        /// LegacySumMax is the historical raw amount sum and highest facet confidence.
        /// Balanced treats each facet as a bounded contribution: amount is the
        /// completeness-weighted average of amount/completenessAmount, scaled to 0-100.
        /// Confidence is the completeness-weighted mean of evidence-backed facet
        /// confidence, with facets having no evidence contributing zero. Evidence
        /// volume is intentionally capped at presence so repeated observations in
        /// one facet cannot make that facet dominate the subject-wide threshold.
        /// </summary>
        internal static KnowledgeStageAggregate Calculate(KnowledgeStageAggregationMode mode,
            IEnumerable<KnowledgeStageFacetValue> values)
        {
            List<KnowledgeStageFacetValue> facets = (values ?? Enumerable.Empty<KnowledgeStageFacetValue>()).ToList();
            if (mode != KnowledgeStageAggregationMode.Balanced)
            {
                return new KnowledgeStageAggregate(facets.Sum(value => Math.Max(0f, value.amount)),
                    facets.Count == 0 ? 0f : facets.Max(value => KnowledgeMath.Clamp01Finite(value.confidence)));
            }

            float totalWeight = 0f;
            float knowledgeTotal = 0f;
            float confidenceTotal = 0f;
            for (int i = 0; i < facets.Count; i++)
            {
                KnowledgeStageFacetValue value = facets[i];
                float weight = KnowledgeMath.PositiveFiniteOr(value.completenessAmount, 0f);
                if (weight <= 0f) continue;
                float progress = KnowledgeMath.Clamp01Finite(value.amount / weight);
                totalWeight += weight;
                knowledgeTotal += progress * weight;
                if (value.evidenceCount > 0)
                    confidenceTotal += KnowledgeMath.Clamp01Finite(value.confidence) * weight;
            }
            if (totalWeight <= 0f) return new KnowledgeStageAggregate(0f, 0f);
            return new KnowledgeStageAggregate(knowledgeTotal / totalWeight * 100f, confidenceTotal / totalWeight);
        }

        internal static KnowledgeStageAggregate ForSubject(GameComponent_KnowledgeFramework component,
            KnowledgeSchema schema, string domainId, string subjectId, Pawn pawn, KnowledgeScope scope,
            KnowledgeContextKey context = default(KnowledgeContextKey),
            KnowledgeContextFallbackMode fallback = KnowledgeContextFallbackMode.ParentThenGlobal)
        {
            if (component == null || schema == null) return new KnowledgeStageAggregate(0f, 0f);
            IReadOnlyList<KnowledgeFacetSchema> applicable = KnowledgeRegistry.ApplicableFacets(domainId, subjectId);
            List<KnowledgeStageFacetValue> values = new List<KnowledgeStageFacetValue>(applicable.Count);
            bool contextual = !context.IsEmpty;
            for (int i = 0; i < applicable.Count; i++)
            {
                KnowledgeFacetSchema facet = applicable[i];
                if (contextual)
                {
                    KnowledgeFacetSnapshotV2 snapshot = KnowledgeQuery.Facet(domainId, subjectId, facet.id, pawn, scope,
                        true, true, context, fallback);
                    values.Add(new KnowledgeStageFacetValue(snapshot.amount, facet.completenessAmount,
                        snapshot.confidence, snapshot.evidenceCount));
                    continue;
                }

                KnowledgeFacetStateRecord record = scope == KnowledgeScope.Colony
                    ? (KnowledgeFacetStateRecord)component.ColonyFacetV2(domainId, subjectId, facet.id, false)
                    : component.PersonalFacetV2(domainId, subjectId, facet.id, pawn, false);
                values.Add(new KnowledgeStageFacetValue(record?.amount ?? 0f, facet.completenessAmount,
                    record == null ? 0f : KnowledgeMath.Confidence(record.supportingEvidence, record.contradictoryEvidence,
                        schema.uncertaintyEnabled), record?.evidenceCount ?? 0));
            }
            return Calculate(schema.stageAggregationMode, values);
        }
    }
}
