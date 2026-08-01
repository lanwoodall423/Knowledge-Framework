using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace KnowledgeFramework
{
    public delegate IEnumerable<KnowledgeComparisonRow> KnowledgeComparisonRowProvider(string domainId, IReadOnlyList<string> subjectIds,
        Pawn pawn, KnowledgeScope scope, KnowledgeContextKey context);

    public static class KnowledgeComparisonService
    {
        private static readonly Dictionary<string, KnowledgeComparisonSchema> Schemas = new Dictionary<string, KnowledgeComparisonSchema>(StringComparer.Ordinal);
        private static readonly Dictionary<string, KnowledgeComparisonRowProvider> Providers = new Dictionary<string, KnowledgeComparisonRowProvider>(StringComparer.Ordinal);

        public static bool RegisterSchema(KnowledgeComparisonSchema schema, bool replace = false)
        {
            if (schema == null || schema.id.NullOrEmpty() || Schemas.ContainsKey(schema.id) && !replace) return false;
            Schemas[schema.id] = schema;
            return true;
        }

        public static bool RegisterRowProvider(string id, KnowledgeComparisonRowProvider provider, bool replace = false)
        {
            if (id.NullOrEmpty() || provider == null || Providers.ContainsKey(id) && !replace) return false;
            Providers[id] = provider;
            return true;
        }

        public static KnowledgeComparisonSnapshot Compare(string domainId, string firstSubjectId, string secondSubjectId,
            Pawn pawn = null, KnowledgeScope scope = KnowledgeScope.Personal, KnowledgeContextKey context = default(KnowledgeContextKey))
        {
            System.Diagnostics.Stopwatch stopwatch = KnowledgeDiagnostics.Enabled ? System.Diagnostics.Stopwatch.StartNew() : null;
            KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId);
            if (schema == null)
            {
                KnowledgeDiagnostics.RecordComparison(stopwatch?.ElapsedTicks ?? 0L);
                return new KnowledgeComparisonSnapshot(domainId, firstSubjectId, secondSubjectId, null, null);
            }
            List<KnowledgeFacetComparison> facets = schema.facets.Select(facet => new KnowledgeFacetComparison(facet.id,
                KnowledgeQuery.Facet(domainId, firstSubjectId, facet.id, pawn, scope, true, false, context, KnowledgeContextFallbackMode.ParentThenGlobal),
                KnowledgeQuery.Facet(domainId, secondSubjectId, facet.id, pawn, scope, true, false, context, KnowledgeContextFallbackMode.ParentThenGlobal))).ToList();
            List<KnowledgeComparisonRow> rows = new List<KnowledgeComparisonRow>();
            IEnumerable<KnowledgeClaimDef> claims = KnowledgeRegistry.ApplicableFacets(domainId, firstSubjectId).SelectMany(facet => KnowledgeRegistry.ApplicableClaims(domainId, firstSubjectId, facet.id))
                .Concat(KnowledgeRegistry.ApplicableFacets(domainId, secondSubjectId).SelectMany(facet => KnowledgeRegistry.ApplicableClaims(domainId, secondSubjectId, facet.id)))
                .GroupBy(claim => claim.StableId).Select(group => group.First());
            foreach (KnowledgeClaimDef claim in claims)
            {
                KnowledgeClaimSnapshot first = KnowledgeClaimService.Snapshot(domainId, firstSubjectId, claim.facetId, claim.StableId, pawn, scope, context,
                    KnowledgeContextFallbackMode.ParentThenGlobal);
                KnowledgeClaimSnapshot second = KnowledgeClaimService.Snapshot(domainId, secondSubjectId, claim.facetId, claim.StableId, pawn, scope, context,
                    KnowledgeContextFallbackMode.ParentThenGlobal);
                rows.Add(new KnowledgeComparisonRow
                {
                    id = "claim:" + claim.StableId,
                    label = claim.LabelCap,
                    kind = KnowledgeComparisonRowKind.Claim,
                    firstKnown = first.observationCount > 0,
                    secondKnown = second.observationCount > 0,
                    firstValue = first.value,
                    secondValue = second.value,
                    firstConfidence = first.effectiveConfidence,
                    secondConfidence = second.effectiveConfidence,
                    summary = first.value?.StableKey() + " | " + second.value?.StableKey()
                });
            }
            string providerId = KnowledgeRegistry.ResolveSubject(domainId, firstSubjectId)?.sourceDef?.defName;
            if (!providerId.NullOrEmpty() && Providers.TryGetValue(providerId, out KnowledgeComparisonRowProvider provider))
            {
                try { rows.AddRange(provider(domainId, new[] { firstSubjectId, secondSubjectId }, pawn, scope, context) ?? Enumerable.Empty<KnowledgeComparisonRow>()); }
                catch (Exception exception) { KnowledgeLog.ErrorOnce("comparison-provider:" + providerId, "A comparison provider failed.", exception); }
            }
            KnowledgeDiagnostics.RecordComparison(stopwatch?.ElapsedTicks ?? 0L);
            return new KnowledgeComparisonSnapshot(domainId, firstSubjectId, secondSubjectId, facets, rows);
        }

        public static KnowledgeStructuredComparisonSnapshot CompareMany(string domainId, IReadOnlyList<string> subjectIds,
            Pawn pawn = null, KnowledgeScope scope = KnowledgeScope.Personal, KnowledgeContextKey context = default(KnowledgeContextKey))
        {
            if (subjectIds == null || subjectIds.Count < 2) return new KnowledgeStructuredComparisonSnapshot(domainId, subjectIds, null);
            string first = subjectIds[0];
            string second = subjectIds[1];
            KnowledgeComparisonSnapshot pair = Compare(domainId, first, second, pawn, scope, context);
            return new KnowledgeStructuredComparisonSnapshot(domainId, subjectIds, pair.rows);
        }
    }
}
