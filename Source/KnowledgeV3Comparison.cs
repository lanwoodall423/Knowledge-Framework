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

        public static KnowledgeComparisonSchema Schema(string id) =>
            !id.NullOrEmpty() && Schemas.TryGetValue(id, out KnowledgeComparisonSchema value) ? value : null;

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
            List<string> ids = (subjectIds ?? Array.Empty<string>()).Where(value => !value.NullOrEmpty()).Distinct().ToList();
            if (ids.Count < 2) return new KnowledgeStructuredComparisonSnapshot(domainId, ids, null);
            KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId);
            if (schema == null) return new KnowledgeStructuredComparisonSnapshot(domainId, ids, null);
            List<KnowledgeComparisonSchema> comparisons = ids.Select(value => ComparisonFor(domainId, value))
                .Where(value => value != null).GroupBy(value => value.id).Select(group => group.First()).ToList();
            IEnumerable<string> facetIds = comparisons.SelectMany(value => value.facetIds ?? new List<string>())
                .Where(value => !value.NullOrEmpty()).Distinct();
            if (!facetIds.Any()) facetIds = schema.facets.Select(value => value.id);
            List<string> comparisonClaimIds = comparisons.SelectMany(value => value.claimIds ?? new List<string>())
                .Where(value => !value.NullOrEmpty()).Distinct().ToList();
            IEnumerable<KnowledgeClaimDef> claims = comparisonClaimIds.Count > 0
                ? schema.claims.Where(value => comparisonClaimIds.Contains(value.StableId))
                : ids.SelectMany(value => KnowledgeRegistry.ApplicableFacets(domainId, value)
                    .SelectMany(facet => KnowledgeRegistry.ApplicableClaims(domainId, value, facet.id)))
                    .GroupBy(value => value.StableId).Select(group => group.First());
            List<KnowledgeComparisonRow> rows = new List<KnowledgeComparisonRow>();
            foreach (string facetId in facetIds.Distinct())
            {
                List<KnowledgeFacetSnapshotV2> values = ids.Select(value => KnowledgeQuery.Facet(domainId, value, facetId, pawn,
                    scope, true, false, context, KnowledgeContextFallbackMode.ParentThenGlobal)).ToList();
                rows.Add(new KnowledgeComparisonRow
                {
                    id = "facet:" + facetId,
                    label = schema.Facet(facetId)?.label ?? facetId,
                    kind = KnowledgeComparisonRowKind.Facet,
                    firstKnown = values[0].amount > 0f,
                    secondKnown = values[1].amount > 0f,
                    firstValue = KnowledgeClaimValue.Float(values[0].amount),
                    secondValue = KnowledgeClaimValue.Float(values[1].amount),
                    firstConfidence = values[0].confidence,
                    secondConfidence = values[1].confidence,
                    values = values.Select(value => KnowledgeClaimValue.Float(value.amount)).ToList(),
                    knownValues = values.Select(value => value.amount > 0f).ToList(),
                    confidences = values.Select(value => value.confidence).ToList(),
                    summary = string.Join(" | ", values.Select(value => value.amount.ToString("0.##")))
                });
            }
            foreach (KnowledgeClaimDef claim in claims.GroupBy(value => value.StableId).Select(group => group.First()))
            {
                List<KnowledgeClaimSnapshot> values = ids.Select(value => KnowledgeClaimService.Snapshot(domainId, value, claim.facetId,
                    claim.StableId, pawn, scope, context, KnowledgeContextFallbackMode.ParentThenGlobal)).ToList();
                rows.Add(new KnowledgeComparisonRow
                {
                    id = "claim:" + claim.StableId,
                    label = claim.LabelCap,
                    kind = KnowledgeComparisonRowKind.Claim,
                    firstKnown = values[0].observationCount > 0,
                    secondKnown = values[1].observationCount > 0,
                    firstValue = values[0].value,
                    secondValue = values[1].value,
                    firstConfidence = values[0].effectiveConfidence,
                    secondConfidence = values[1].effectiveConfidence,
                    values = values.Select(value => value.value).ToList(),
                    knownValues = values.Select(value => value.observationCount > 0).ToList(),
                    confidences = values.Select(value => value.effectiveConfidence).ToList(),
                    summary = string.Join(" | ", values.Select(value => value.value?.StableKey() ?? "unknown"))
                });
            }
            string providerId = KnowledgeRegistry.ResolveSubject(domainId, ids[0])?.sourceDef?.defName;
            if (!providerId.NullOrEmpty() && Providers.TryGetValue(providerId, out KnowledgeComparisonRowProvider provider))
            {
                try { rows.AddRange(provider(domainId, ids, pawn, scope, context) ?? Enumerable.Empty<KnowledgeComparisonRow>()); }
                catch (Exception exception) { KnowledgeLog.ErrorOnce("comparison-provider:" + providerId, "A comparison provider failed.", exception); }
            }
            return new KnowledgeStructuredComparisonSnapshot(domainId, ids, rows);
        }

        private static KnowledgeComparisonSchema ComparisonFor(string domainId, string subjectId)
        {
            KnowledgeSubjectSnapshot subject = KnowledgeRegistry.ResolveSubject(domainId, subjectId);
            KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId);
            string comparisonId = schema?.Archetype(subject?.archetypeId)?.comparisonSchemaId;
            return Schema(comparisonId);
        }
    }
}
