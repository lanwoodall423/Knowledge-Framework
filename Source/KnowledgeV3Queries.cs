using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace KnowledgeFramework
{
    public static partial class KnowledgeQuery
    {
        /// <summary>Monotonic game knowledge revision for cached consumer view models.</summary>
        public static int Revision => GameComponent_KnowledgeFramework.Current?.GlobalRevision ?? 0;

        public static KnowledgeFacetSnapshotV2 Facet(string domainId, string subjectId, string facetId, Pawn pawn,
            KnowledgeScope scope, bool includeDerived, bool includeEvidenceDetails, KnowledgeContextKey context,
            KnowledgeContextFallbackMode fallback)
        {
            if (context.IsPartial)
                return new KnowledgeFacetSnapshotV2(domainId, subjectId, facetId, scope == KnowledgeScope.Colony ? null : pawn, scope,
                    0f, 0f, 0f, 0f, true, 0, 0, 0, 0, null, null, null, context, false, 0);
            domainId = KnowledgeRegistry.ResolveDomainId(domainId);
            subjectId = KnowledgeRegistry.ResolveSubjectId(domainId, subjectId);
            KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId);
            facetId = facetId.NullOrEmpty() ? KnowledgeSchema.DefaultFacetId : facetId;
            if (schema != null && facetId == KnowledgeSchema.DefaultFacetId && schema.Facet(facetId) == null) facetId = schema.facets.FirstOrDefault()?.id;
            KnowledgeFacetSchema facet = schema?.Facet(facetId);
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            if (schema == null || facet == null || component == null)
            {
                if (context.IsEmpty && !context.IsPartial) return Facet(domainId, subjectId, facetId, pawn, scope, includeDerived, includeEvidenceDetails);
                return new KnowledgeFacetSnapshotV2(domainId, subjectId, facetId, scope == KnowledgeScope.Colony ? null : pawn, scope,
                    0f, 0f, 0f, 0f, true, 0, 0, 0, 0, null, null, null, context, false, 0);
            }
            bool colony = scope == KnowledgeScope.Colony;
            KnowledgeContextFacetStateRecord record = null;
            foreach (KnowledgeContextKey candidate in KnowledgeContextRegistry.Chain(context, fallback))
            {
                record = component.ContextFacetV3(domainId, subjectId, facetId, colony ? null : pawn, colony, candidate, false);
                if (record != null && (record.amount > 0f || record.evidenceCount > 0 || record.supportingEvidence > 0f || record.contradictoryEvidence > 0f)) break;
            }
            if (record == null || record.amount <= 0f && record.evidenceCount <= 0 && record.supportingEvidence <= 0f && record.contradictoryEvidence <= 0f)
            {
                // The empty context is the persisted global key. Legacy V2 data is a
                // compatibility fallback only when that exact global key has no value;
                // a contextual query must never silently become a legacy query.
                if (context.IsEmpty && !context.IsPartial) return Facet(domainId, subjectId, facetId, pawn, scope, includeDerived, includeEvidenceDetails);
                return new KnowledgeFacetSnapshotV2(domainId, subjectId, facetId, colony ? null : pawn, scope, 0f, 0f, 0f, 0f,
                    true, 0, 0, 0, 0, null, null, null, context, false, 0);
            }
            float confidence = KnowledgeMath.Confidence(record.supportingEvidence, record.contradictoryEvidence, schema.uncertaintyEnabled);
            return new KnowledgeFacetSnapshotV2(domainId, subjectId, facetId, colony ? null : pawn, scope, record.amount, 0f,
                Math.Min(1f, record.amount / facet.completenessAmount), confidence, false, record.evidenceCount,
                record.successCount, record.failureCount, record.revision, null, null, null,
                new KnowledgeContextKey(record.contextTypeId, record.contextId),
                !context.IsEmpty && !new KnowledgeContextKey(record.contextTypeId, record.contextId).Equals(context),
                record.lastTick);
        }

        public static IReadOnlyList<KnowledgeClaimSnapshot> Claims(string domainId, string subjectId, string facetId = null,
            Pawn pawn = null, KnowledgeScope scope = KnowledgeScope.Personal, KnowledgeContextKey context = default(KnowledgeContextKey),
            KnowledgeContextFallbackMode fallback = KnowledgeContextFallbackMode.ExactOnly) =>
            KnowledgeClaimService.ForSubject(domainId, subjectId, facetId, pawn, scope, context, fallback);

        public static IReadOnlyList<KnowledgeFacetSchema> ApplicableFacets(string domainId, string subjectId) =>
            KnowledgeRegistry.ApplicableFacets(domainId, subjectId);

        public static IReadOnlyList<KnowledgeSubjectRelation> StructuralRelations(string domainId, string subjectId = null,
            bool outgoing = true, bool incoming = true, KnowledgeContextKey context = default(KnowledgeContextKey)) =>
            KnowledgeRelationService.Query(domainId, subjectId, outgoing, incoming, context);

        public static IReadOnlyList<KnowledgeMilestoneState> Milestones(string domainId, string subjectId, Pawn pawn = null,
            KnowledgeContextKey context = default(KnowledgeContextKey)) => KnowledgeMilestoneService.States(domainId, subjectId, pawn, context);
    }

    public static class KnowledgeContextQuery
    {
        public static KnowledgeFacetSnapshotV2 Facet(string domainId, string subjectId, string facetId, KnowledgeContextKey context,
            Pawn pawn = null, KnowledgeScope scope = KnowledgeScope.Personal, KnowledgeContextFallbackMode fallback = KnowledgeContextFallbackMode.ParentThenGlobal) =>
            KnowledgeQuery.Facet(domainId, subjectId, facetId, pawn, scope, true, true, context, fallback);

        public static KnowledgeClaimSnapshot Claim(string domainId, string subjectId, string facetId, string claimId, KnowledgeContextKey context,
            Pawn pawn = null, KnowledgeScope scope = KnowledgeScope.Personal, KnowledgeContextFallbackMode fallback = KnowledgeContextFallbackMode.ParentThenGlobal) =>
            KnowledgeClaimService.Snapshot(domainId, subjectId, facetId, claimId, pawn, scope, context, fallback);
    }
}
