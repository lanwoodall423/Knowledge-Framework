using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;
using Verse;

namespace KnowledgeFramework
{
    public enum KnowledgeBrowserSort
    {
        Label,
        Stage,
        Confidence,
        Recency,
        Completeness
    }

    public sealed class KnowledgeBrowserFilter
    {
        public string domainId;
        public Pawn pawn;
        public KnowledgeScope scope = KnowledgeScope.Personal;
        public string search;
        public string categoryId;
        public string facetId;
        public KnowledgeContextKey context;
        public bool includeUnknown;
        public bool includeArchived;
        public bool includeHidden;
        public bool includeMissingContent;
        public bool developerMode;
        public KnowledgeContextFallbackMode fallback = KnowledgeContextFallbackMode.ParentThenGlobal;
        public KnowledgeBrowserSort sort = KnowledgeBrowserSort.Label;
    }

    public sealed class KnowledgeBrowserRow
    {
        public readonly KnowledgeSubjectSnapshot subject;
        public readonly KnowledgeSubjectSnapshotV2 state;
        public readonly IReadOnlyList<KnowledgeFacetSchema> applicableFacets;
        public readonly float completeness;
        public readonly float confidence;
        public readonly int evidenceCount;
        public readonly int recencyTick;
        public readonly bool usedContextFallback;
        public readonly KnowledgeStageProvenance stageProvenance;
        public readonly KnowledgeContextKey requestedContext;
        public readonly KnowledgeContextKey resolvedContext;
        public readonly string lastStage;
        public readonly string currentStageId;
        public readonly string displayLabel;
        public readonly string displayDescription;
        public readonly bool identified;
        public readonly IReadOnlyDictionary<string, KnowledgeFacetSnapshotV2> facetValues;
        public readonly IReadOnlyList<KnowledgeClaimSnapshot> claims;
        public readonly IReadOnlyList<KnowledgeInsightProgress> insights;
        public readonly IReadOnlyList<string> unmetRequirements;
        public readonly int unmetRequirementCount;
        public readonly IReadOnlyList<KnowledgeMilestoneState> milestones;
        public readonly IReadOnlyList<KnowledgeSubjectRelation> relations;
        public readonly IReadOnlyList<string> badges;

        internal KnowledgeBrowserRow(KnowledgeSubjectSnapshot subject, KnowledgeSubjectSnapshotV2 state,
            IEnumerable<KnowledgeFacetSchema> applicableFacets, float completeness, float confidence, int evidenceCount,
            int recencyTick, bool usedContextFallback, IEnumerable<KnowledgeMilestoneState> milestones,
             IEnumerable<KnowledgeSubjectRelation> relations, IEnumerable<string> badges,
             KnowledgeContextKey requestedContext, KnowledgeContextKey resolvedContext, string displayLabel,
             string displayDescription, bool identified, string currentStageId, IDictionary<string, KnowledgeFacetSnapshotV2> facetValues,
             IEnumerable<KnowledgeClaimSnapshot> claims, IEnumerable<KnowledgeInsightProgress> insights,
             IEnumerable<string> unmetRequirements, KnowledgeStageProvenance stageProvenance = KnowledgeStageProvenance.None)
        {
            this.subject = subject;
            this.state = state;
            this.applicableFacets = new ReadOnlyCollection<KnowledgeFacetSchema>((applicableFacets ?? Enumerable.Empty<KnowledgeFacetSchema>()).ToList());
            this.completeness = completeness;
            this.confidence = confidence;
            this.evidenceCount = evidenceCount;
            this.recencyTick = recencyTick;
            this.usedContextFallback = usedContextFallback;
            this.stageProvenance = stageProvenance;
            this.requestedContext = requestedContext;
            this.resolvedContext = resolvedContext;
            this.currentStageId = currentStageId.NullOrEmpty() ? state.stageId : currentStageId;
            lastStage = this.currentStageId;
            this.displayLabel = displayLabel.NullOrEmpty() ? "KnowledgeFramework_Unknown".Translate() : displayLabel;
            this.displayDescription = displayDescription.NullOrEmpty() ? "KnowledgeFramework_Unknown".Translate() : displayDescription;
            this.identified = identified;
            this.facetValues = new ReadOnlyDictionary<string, KnowledgeFacetSnapshotV2>(
                new Dictionary<string, KnowledgeFacetSnapshotV2>(facetValues ?? new Dictionary<string, KnowledgeFacetSnapshotV2>(), StringComparer.Ordinal));
            this.claims = new ReadOnlyCollection<KnowledgeClaimSnapshot>((claims ?? Enumerable.Empty<KnowledgeClaimSnapshot>()).ToList());
            this.insights = new ReadOnlyCollection<KnowledgeInsightProgress>((insights ?? Enumerable.Empty<KnowledgeInsightProgress>()).ToList());
            List<string> safeRequirements = (unmetRequirements ?? Enumerable.Empty<string>()).ToList();
            unmetRequirementCount = safeRequirements.Count;
            this.unmetRequirements = safeRequirements.Count == 0
                ? new ReadOnlyCollection<string>(new List<string>())
                : new ReadOnlyCollection<string>(new List<string>
                {
                    "KnowledgeFramework_RequirementsUnmet".Translate(safeRequirements.Count)
                });
            this.milestones = new ReadOnlyCollection<KnowledgeMilestoneState>((milestones ?? Enumerable.Empty<KnowledgeMilestoneState>()).ToList());
            this.relations = new ReadOnlyCollection<KnowledgeSubjectRelation>((relations ?? Enumerable.Empty<KnowledgeSubjectRelation>()).ToList());
            this.badges = new ReadOnlyCollection<string>((badges ?? Enumerable.Empty<string>()).ToList());
        }
    }

    public static class KnowledgeBrowserLabels
    {
        public static string Subject(string domainId, string subjectId, Pawn pawn = null,
            KnowledgeScope scope = KnowledgeScope.Personal, KnowledgeContextKey context = default(KnowledgeContextKey),
            bool developerMode = false)
        {
            KnowledgeSubjectSnapshot subject = KnowledgeRegistry.ResolveSubject(domainId, subjectId);
            if (subject == null) return "KnowledgeFramework_Unknown".Translate();
            KnowledgeRevealResult presentation = KnowledgeDiscovery.Present(domainId, subjectId, null, pawn, scope, context,
                KnowledgeContextFallbackMode.ParentThenGlobal);
            return SubjectLabel(subject, presentation, developerMode);
        }

        public static string SubjectLabel(KnowledgeSubjectSnapshot subject, KnowledgeRevealResult presentation, bool developerMode = false)
        {
            if (subject == null) return "KnowledgeFramework_Unknown".Translate();
            if (developerMode) return Label(subject.label, "KnowledgeFramework_Unknown");
            if (presentation == null || presentation.label.NullOrEmpty() || presentation.label == subject.id)
                return "KnowledgeFramework_Unknown".Translate();
            return presentation.label;
        }

        public static string SubjectDescription(KnowledgeSubjectSnapshot subject, KnowledgeRevealResult presentation,
            bool developerMode = false)
        {
            if (subject == null) return "KnowledgeFramework_Unknown".Translate();
            if (developerMode) return Label(subject.description, "KnowledgeFramework_Unknown");
            if (presentation == null || presentation.description.NullOrEmpty() || presentation.description == subject.id)
                return "KnowledgeFramework_Unknown".Translate();
            return presentation.description;
        }

        public static string Facet(KnowledgeFacetSchema facet) => facet == null || facet.label == facet.id
            ? "KnowledgeFramework_Unknown".Translate() : Label(facet.label, "KnowledgeFramework_Unknown");

        public static string Stage(KnowledgeSchema schema, string stageId)
        {
            KnowledgeStageSchema stage = schema?.Stage(stageId);
            return stage == null || stage.label == stage.id ? "KnowledgeFramework_Unknown".Translate() :
                Label(stage.label, "KnowledgeFramework_Unknown");
        }

        public static string StageProvenance(KnowledgeStageProvenance provenance)
        {
            switch (provenance)
            {
                case KnowledgeStageProvenance.CalculatedExact: return "KnowledgeFramework_StageSourceCalculatedExact".Translate();
                case KnowledgeStageProvenance.PersistedExact: return "KnowledgeFramework_StageSourcePersistedExact".Translate();
                case KnowledgeStageProvenance.InheritedParent: return "KnowledgeFramework_StageSourceInheritedParent".Translate();
                case KnowledgeStageProvenance.InheritedGlobal: return "KnowledgeFramework_StageSourceInheritedGlobal".Translate();
                default: return null;
            }
        }

        public static string Claim(KnowledgeSchema schema, string claimId)
        {
            KnowledgeClaimDef claim = schema?.claims.FirstOrDefault(item => item?.StableId == claimId);
            return claim == null || claim.label.NullOrEmpty() || claim.label == claim.StableId
                ? "KnowledgeFramework_Unknown".Translate() : Label(claim.label, "KnowledgeFramework_Unknown");
        }

        public static string Insight(KnowledgeSchema schema, string insightId)
        {
            KnowledgeInsightDef insight = schema?.insights.FirstOrDefault(item => item?.defName == insightId);
            return insight == null || insight.label.NullOrEmpty() || insight.label == insight.defName
                ? "KnowledgeFramework_Unknown".Translate() : Label(insight.label, "KnowledgeFramework_Unknown");
        }

        public static string Milestone(KnowledgeSchema schema, string trackId, string milestoneId)
        {
            KnowledgeMilestoneTrackDef track = schema?.milestoneTracks.FirstOrDefault(item => item?.StableId == trackId);
            KnowledgeMilestoneDef milestone = track?.milestones?.FirstOrDefault(item => item?.StableId == milestoneId);
            return milestone == null || milestone.label.NullOrEmpty() || milestone.label == milestone.StableId
                ? "KnowledgeFramework_Unknown".Translate() : Label(milestone.label, "KnowledgeFramework_Unknown");
        }

        public static string Track(KnowledgeSchema schema, string trackId)
        {
            KnowledgeMilestoneTrackDef track = schema?.milestoneTracks.FirstOrDefault(item => item?.StableId == trackId);
            return track == null || track.label.NullOrEmpty() || track.label == track.StableId
                ? "KnowledgeFramework_Unknown".Translate() : Label(track.label, "KnowledgeFramework_Unknown");
        }

        public static string RelationType(string relationTypeId)
        {
            KnowledgeSubjectRelationTypeDef type = KnowledgeRelationService.Type(relationTypeId);
            return type == null || type.label.NullOrEmpty() || type.label == type.StableId
                ? "KnowledgeFramework_Unknown".Translate() : Label(type.label, "KnowledgeFramework_Unknown");
        }

        public static string Context(KnowledgeContextKey context)
        {
            if (context.IsEmpty) return "KnowledgeFramework_GlobalContext".Translate();
            KnowledgeContextTypeDef type = KnowledgeContextRegistry.Type(context.typeId);
            return type == null || type.label.NullOrEmpty() || type.label == type.StableId
                ? "KnowledgeFramework_Unknown".Translate() : Label(type.label, "KnowledgeFramework_Unknown");
        }

        public static string ContextValue(KnowledgeContextKey context, string domainId = null, string subjectId = null,
            Pawn pawn = null, KnowledgeScope scope = KnowledgeScope.Personal)
        {
            if (context.IsEmpty) return "KnowledgeFramework_GlobalContext".Translate();
            string value = KnowledgeContextRegistry.ValueLabel(context, domainId, subjectId, pawn, scope)?.Trim();
            // Presentation providers return already-human-readable values. A
            // dynamic value is not a translation key; translating it can
            // transform arbitrary provider labels into unrelated text.
            return value.NullOrEmpty() || value == context.stableId || value == context.typeId || value == context.ToString()
                ? "KnowledgeFramework_UnknownContextValue".Translate() : value;
        }

        public static string ContextSelection(KnowledgeContextKey context, string domainId = null, string subjectId = null,
            Pawn pawn = null, KnowledgeScope scope = KnowledgeScope.Personal)
        {
            if (context.IsEmpty) return "KnowledgeFramework_GlobalContext".Translate();
            return "KnowledgeFramework_ContextSelection".Translate(Context(context),
                ContextValue(context, domainId, subjectId, pawn, scope));
        }

        public static string Rank(KnowledgeRank rank) => ("KnowledgeFramework_Rank_" + rank).Translate();

        private static string Label(string value, string fallbackKey)
        {
            if (value.NullOrEmpty()) return fallbackKey.Translate();
            // Registration labels may be either localization keys or literal
            // consumer text. Only translate keys present in the active language;
            // arbitrary labels such as "Size" must remain unchanged.
            bool looksLikeTranslationKey = value.IndexOf('_') >= 0 || value.IndexOf('.') >= 0;
            return looksLikeTranslationKey && LanguageDatabase.activeLanguage != null && LanguageDatabase.activeLanguage.HaveTextForKey(value, false)
                ? value.Translate() : value;
        }
    }

    public static class KnowledgeBrowserModels
    {
        public static IReadOnlyList<KnowledgeContextKey> ContextOptions(KnowledgeBrowserFilter filter, string subjectId = null,
            bool includeRequestedContext = true)
        {
            if (filter == null || filter.domainId.NullOrEmpty()) return new[] { KnowledgeContextKey.Empty };
            KnowledgeSchema schema = KnowledgeRegistry.Schema(filter.domainId);
            if (schema == null) return new[] { KnowledgeContextKey.Empty };
            Pawn pawn = filter.scope == KnowledgeScope.Colony ? null : filter.pawn;
            List<KnowledgeContextKey> persisted = new List<KnowledgeContextKey>();
            GameComponent_KnowledgeFramework component = GameComponent_KnowledgeFramework.Current;
            if (component != null && !subjectId.NullOrEmpty())
                persisted.AddRange(component.ContextKeysV3(schema.id, subjectId, pawn, filter.scope == KnowledgeScope.Colony));
            List<KnowledgeContextKey> result = new List<KnowledgeContextKey> { KnowledgeContextKey.Empty };
            foreach (KnowledgeContextKey candidate in KnowledgeContextRegistry.KnownContexts(schema.id, subjectId, pawn,
                filter.scope, persisted))
                if (filter.developerMode || ContextAuthorized(filter, subjectId, candidate)) result.Add(candidate);
            if (includeRequestedContext && !filter.context.IsEmpty && !filter.context.IsPartial &&
                KnowledgeContextRegistry.Type(filter.context.typeId) != null &&
                (filter.developerMode || ContextAuthorized(filter, subjectId, filter.context))) result.Add(filter.context);
            return new[] { KnowledgeContextKey.Empty }.Concat(KnowledgeContextRegistry.OrderContexts(result)).ToList();
        }

        internal static bool ContextAuthorized(KnowledgeBrowserFilter filter, string subjectId, KnowledgeContextKey context)
        {
            if (filter == null || context.IsEmpty || context.IsPartial) return context.IsEmpty;
            Pawn pawn = filter.scope == KnowledgeScope.Colony ? null : filter.pawn;
            if (KnowledgeContextRegistry.IsProviderKnownContext(context, filter.domainId, subjectId, pawn, filter.scope)) return true;
            // A child context can legitimately inherit knowledge from an
            // authorized parent context. Authorize the requested child when
            // its fallback chain contains a provider-known parent; otherwise
            // browser queries would silently collapse to global knowledge.
            foreach (KnowledgeContextKey candidate in KnowledgeContextRegistry.Chain(context, filter.fallback))
                if (!candidate.IsEmpty && !candidate.Equals(context) &&
                    KnowledgeContextRegistry.IsProviderKnownContext(candidate, filter.domainId, subjectId, pawn, filter.scope))
                    return true;
            KnowledgeSchema schema = KnowledgeRegistry.Schema(filter.domainId);
            if (schema == null) return false;

            foreach (KnowledgeFacetSchema facet in KnowledgeRegistry.ApplicableFacets(schema.id, subjectId))
            {
                if (facet.hiddenUntilRevealed && !KnowledgeDiscovery.Present(schema.id, subjectId, facet.id, pawn,
                    filter.scope, context, KnowledgeContextFallbackMode.ExactOnly).revealed) continue;
                KnowledgeFacetSnapshotV2 snapshot = KnowledgeQuery.Facet(schema.id, subjectId, facet.id, pawn, filter.scope,
                    true, true, context, KnowledgeContextFallbackMode.ExactOnly);
                if (snapshot == null || !snapshot.context.Equals(context)) continue;
                if (snapshot.amount > 0f || snapshot.evidenceCount > 0 || snapshot.successCount > 0 ||
                    snapshot.failureCount > 0 || snapshot.lastTick > 0 || snapshot.EventCounts.Count > 0) return true;
            }

            foreach (KnowledgeFacetSchema facet in KnowledgeRegistry.ApplicableFacets(schema.id, subjectId))
                foreach (KnowledgeClaimDef claim in KnowledgeRegistry.ApplicableClaims(schema.id, subjectId, facet.id))
                {
                    if (facet.hiddenUntilRevealed && !KnowledgeDiscovery.Present(schema.id, subjectId, facet.id, pawn,
                        filter.scope, context, KnowledgeContextFallbackMode.ExactOnly).revealed) continue;
                    KnowledgeClaimSnapshot snapshot = KnowledgeClaimService.Snapshot(schema.id, subjectId, facet.id,
                        claim.StableId, pawn, filter.scope, context, KnowledgeContextFallbackMode.ExactOnly);
                    if (snapshot == null || snapshot.observationCount <= 0 || !snapshot.context.Equals(context)) continue;
                    if (claim.revealedByDefault || snapshot.revealed) return true;
                }
            return false;
        }

        public static bool HasContextualKnowledge(KnowledgeBrowserFilter filter, string subjectId = null)
        {
            KnowledgeSchema schema = filter == null ? null : KnowledgeRegistry.Schema(filter.domainId);
            return schema != null && (schema.stages.Any(item => item.contextSensitive) ||
                ContextOptions(filter, subjectId).Count > 1);
        }

        public static IReadOnlyList<KnowledgeBrowserRow> Build(KnowledgeBrowserFilter filter)
        {
            if (filter == null || filter.domainId.NullOrEmpty()) return Array.Empty<KnowledgeBrowserRow>();
            KnowledgeSchema schema = KnowledgeRegistry.Schema(filter.domainId);
            if (schema == null) return Array.Empty<KnowledgeBrowserRow>();
            string search = filter.search?.Trim();
            List<KnowledgeBrowserRow> result = new List<KnowledgeBrowserRow>();
            foreach (KnowledgeSubjectSnapshot subject in KnowledgeRegistry.Subjects(schema.id))
            {
                KnowledgeBrowserRow row = BuildSubject(filter, subject.id, subject, schema);
                if (row == null) continue;
                if (!search.NullOrEmpty() && !MatchesSearch(row, search, filter.developerMode)) continue;
                result.Add(row);
            }
            switch (filter.sort)
            {
                case KnowledgeBrowserSort.Stage: return result.OrderByDescending(item => schema.Stage(item.currentStageId)?.order ?? int.MinValue).ThenBy(item => item.displayLabel).ToList();
                case KnowledgeBrowserSort.Confidence: return result.OrderByDescending(item => item.confidence).ThenBy(item => item.displayLabel).ToList();
                case KnowledgeBrowserSort.Completeness: return result.OrderByDescending(item => item.completeness).ThenBy(item => item.displayLabel).ToList();
                case KnowledgeBrowserSort.Recency: return result.OrderByDescending(item => item.recencyTick).ThenBy(item => item.displayLabel).ToList();
                default: return result.OrderBy(item => item.displayLabel).ThenBy(item => item.subject.id).ToList();
            }
        }

        public static KnowledgeBrowserRow BuildSubject(KnowledgeBrowserFilter filter, string subjectId)
        {
            if (filter == null || filter.domainId.NullOrEmpty() || subjectId.NullOrEmpty()) return null;
            KnowledgeSchema schema = KnowledgeRegistry.Schema(filter.domainId);
            KnowledgeSubjectSnapshot subject = KnowledgeRegistry.ResolveSubject(filter.domainId, subjectId);
            return schema == null || subject == null ? null : BuildSubject(filter, subject.id, subject, schema);
        }

        private static KnowledgeBrowserRow BuildSubject(KnowledgeBrowserFilter filter, string subjectId,
            KnowledgeSubjectSnapshot subject, KnowledgeSchema schema)
        {
            if (!LifecycleVisible(subject, filter)) return null;
            if (!filter.categoryId.NullOrEmpty() && !(subject.categoryIds ?? Array.Empty<string>()).Contains(filter.categoryId)) return null;

            KnowledgeContextKey requestedContext = filter.context;
            KnowledgeContextKey visibleContext = filter.developerMode || ContextAuthorized(filter, subjectId, requestedContext)
                ? requestedContext : KnowledgeContextKey.Empty;
            KnowledgeBrowserFilter queryFilter = WithContext(filter, visibleContext);

            KnowledgeRevealResult presentation = KnowledgeDiscovery.Present(schema.id, subjectId, null, filter.pawn, filter.scope,
                queryFilter.context, queryFilter.fallback);
            KnowledgeStageSnapshot stage = KnowledgeDiscovery.StageSnapshot(schema.id, subjectId, filter.pawn, filter.scope,
                queryFilter.context, queryFilter.fallback);
            if (!filter.developerMode && !filter.includeUnknown && !presentation.identified) return null;

            IReadOnlyList<KnowledgeFacetSchema> allFacets = KnowledgeRegistry.ApplicableFacets(schema.id, subjectId);
            List<KnowledgeFacetSchema> facets = allFacets.Where(facet => FacetVisible(schema.id, subjectId, facet, queryFilter)).ToList();
            if (!filter.facetId.NullOrEmpty() && !facets.Any(item => item.id == filter.facetId)) return null;
            KnowledgeSubjectSnapshotV2 state = KnowledgeQuery.Subject(schema.id, subjectId, filter.pawn, filter.scope);
            Dictionary<string, KnowledgeFacetSnapshotV2> values = facets.ToDictionary(item => item.id, item => KnowledgeQuery.Facet(
                schema.id, subjectId, item.id, filter.pawn, filter.scope, true, true, queryFilter.context, queryFilter.fallback), StringComparer.Ordinal);
            float[] weights = facets.Select(item => Math.Max(1f, item.completenessAmount)).ToArray();
            KnowledgeFacetSnapshotV2[] snapshots = facets.Select(item => values[item.id]).ToArray();
            float totalWeight = weights.Sum();
            float completeness = snapshots.Length == 0 || totalWeight <= 0f ? 0f :
                snapshots.Select((item, index) => item.completeness * weights[index]).Sum() / totalWeight;
            float confidenceWeight = snapshots.Select((item, index) => item.evidenceCount > 0
                ? Math.Max(0.25f, item.completeness) * weights[index] : 0f).Sum();
            float confidence = snapshots.Length == 0 || confidenceWeight <= 0f ? 0f :
                snapshots.Select((item, index) => item.evidenceCount > 0
                    ? item.confidence * Math.Max(0.25f, item.completeness) * weights[index] : 0f).Sum() / confidenceWeight;
            int recency = snapshots.Select(item => item.lastTick).Concat(snapshots.SelectMany(item =>
                item.provenance ?? Array.Empty<KnowledgeProvenanceSnapshot>()).Select(item => item.tick)).DefaultIfEmpty(0).Max();
            int evidenceCount = snapshots.Sum(item => item.evidenceCount);
            List<KnowledgeClaimSnapshot> claims = facets.SelectMany(facet => KnowledgeClaimService.ForSubject(schema.id, subjectId, facet.id,
                    filter.pawn, filter.scope, queryFilter.context, queryFilter.fallback)).Where(claim =>
                filter.developerMode || schema.Claim(claim.claimId)?.revealedByDefault == true || claim.revealed).ToList();
            List<KnowledgeMilestoneState> milestones = Milestones(schema, subjectId, queryFilter);
            List<KnowledgeSubjectRelation> relations = Relations(schema, subjectId, queryFilter);
            List<KnowledgeInsightProgress> insights = schema.insights.Select(insight => KnowledgeInsightService.Progress(
                insight.defName, schema.id, subjectId, filter.pawn, filter.scope, queryFilter.context)).ToList();
            KnowledgeStageSchema nextStage = NextStage(schema, stage?.stageId);
            IReadOnlyList<string> unmet = nextStage == null ? Array.Empty<string>() : KnowledgeDiscovery.UnmetStageRequirements(
                schema.id, subjectId, nextStage.id, filter.pawn, filter.scope, queryFilter.context, filter.fallback);
            bool usedFallback = stage?.usedContextFallback == true || snapshots.Any(item => item.usedContextFallback) || claims.Any(item =>
                !queryFilter.context.IsEmpty && !item.context.Equals(queryFilter.context)) || milestones.Any(item =>
                !queryFilter.context.IsEmpty && !item.context.Equals(queryFilter.context)) || relations.Any(item =>
                !queryFilter.context.IsEmpty && !item.context.Equals(queryFilter.context));
            KnowledgeContextKey resolvedContext = queryFilter.context;
            bool hasResolvedContext = false;
            foreach (KnowledgeContextKey candidate in snapshots.Select(item => item.context).Concat(claims.Select(item => item.context))
                .Concat(milestones.Select(item => item.context)).Concat(relations.Select(item => item.context)))
            {
                if (candidate.Equals(queryFilter.context)) continue;
                resolvedContext = candidate;
                hasResolvedContext = true;
                break;
            }
            if (!hasResolvedContext) resolvedContext = queryFilter.context;
            if (stage != null && stage.usedContextFallback)
            {
                resolvedContext = stage.resolvedContext;
                hasResolvedContext = true;
            }
            if (!filter.developerMode && !filter.includeUnknown && !presentation.identified && completeness <= 0f) return null;
            string displayLabel = KnowledgeBrowserLabels.SubjectLabel(subject, presentation, filter.developerMode);
            string displayDescription = KnowledgeBrowserLabels.SubjectDescription(subject, presentation, filter.developerMode);
            string badge = subject.state == KnowledgeSubjectState.Archived || subject.state == KnowledgeSubjectState.Retired
                ? "KnowledgeFramework_Archived".Translate() : subject.state == KnowledgeSubjectState.MissingContent
                    ? "KnowledgeFramework_MissingContent".Translate() : subject.state == KnowledgeSubjectState.Hidden
                        ? "KnowledgeFramework_Hidden".Translate() : null;
            return new KnowledgeBrowserRow(subject, state, facets, completeness, confidence, evidenceCount, recency, usedFallback, milestones,
                relations, badge.NullOrEmpty() ? null : new[] { badge }, queryFilter.context, resolvedContext, displayLabel, displayDescription,
                presentation.identified, stage?.stageId ?? presentation.stageId, values, claims, insights, unmet,
                stage?.provenance ?? KnowledgeStageProvenance.None);
        }

        private static KnowledgeBrowserFilter WithContext(KnowledgeBrowserFilter source, KnowledgeContextKey context)
        {
            return new KnowledgeBrowserFilter
            {
                domainId = source.domainId,
                pawn = source.pawn,
                scope = source.scope,
                search = source.search,
                categoryId = source.categoryId,
                facetId = source.facetId,
                context = context,
                includeUnknown = source.includeUnknown,
                includeArchived = source.includeArchived,
                includeHidden = source.includeHidden,
                includeMissingContent = source.includeMissingContent,
                developerMode = source.developerMode,
                fallback = source.fallback,
                sort = source.sort
            };
        }

        private static bool LifecycleVisible(KnowledgeSubjectSnapshot subject, KnowledgeBrowserFilter filter)
        {
            if (subject == null) return false;
            if (filter.developerMode) return true;
            if ((subject.state == KnowledgeSubjectState.Archived || subject.state == KnowledgeSubjectState.Retired) && !filter.includeArchived) return false;
            if (subject.state == KnowledgeSubjectState.Hidden && !filter.includeHidden) return false;
            if (subject.state == KnowledgeSubjectState.MissingContent && !filter.includeMissingContent) return false;
            return true;
        }

        private static bool FacetVisible(string domainId, string subjectId, KnowledgeFacetSchema facet, KnowledgeBrowserFilter filter)
        {
            if (facet == null) return false;
            if (!facet.hiddenUntilRevealed) return true;
            KnowledgeRevealResult presentation = KnowledgeDiscovery.Present(domainId, subjectId, facet.id, filter.pawn, filter.scope,
                filter.context, filter.fallback);
            if (presentation.revealed) return true;
            // Developer mode is an explicit debug escape hatch. A normal
            // includeHidden request remains harmless unless the caller also
            // has developer mode, so player-facing filters cannot disclose it.
            return filter.developerMode;
        }

        private static KnowledgeStageSchema NextStage(KnowledgeSchema schema, string currentStageId)
        {
            int order = schema.Stage(currentStageId)?.order ?? int.MinValue;
            return schema.stages.Where(item => item.order > order).OrderBy(item => item.order).FirstOrDefault();
        }

        private static List<KnowledgeMilestoneState> Milestones(KnowledgeSchema schema, string subjectId, KnowledgeBrowserFilter filter)
        {
            List<KnowledgeMilestoneState> result = new List<KnowledgeMilestoneState>();
            foreach (KnowledgeMilestoneTrackDef track in schema.milestoneTracks)
                foreach (KnowledgeMilestoneDef milestone in track.milestones ?? new List<KnowledgeMilestoneDef>())
                {
                    KnowledgeMilestoneState selected = null;
                    foreach (KnowledgeContextKey candidate in KnowledgeContextRegistry.Chain(filter.context, filter.fallback))
                    {
                        KnowledgeMilestoneState value = KnowledgeMilestoneService.State(schema.id, subjectId, track.StableId,
                            milestone.StableId, filter.pawn, candidate);
                        if (HasMilestoneState(value))
                        {
                            selected = value;
                            break;
                        }
                    }
                    result.Add(selected ?? KnowledgeMilestoneService.State(schema.id, subjectId, track.StableId,
                        milestone.StableId, filter.pawn, filter.context));
                }
            return result;
        }

        private static bool HasMilestoneState(KnowledgeMilestoneState state) => state != null &&
            (state.available || state.started || state.interrupted || state.completed || state.progress > 0f ||
                state.bestHistoricalValue > 0f || state.startTick > 0 || state.completionTick > 0);

        private static List<KnowledgeSubjectRelation> Relations(KnowledgeSchema schema, string subjectId, KnowledgeBrowserFilter filter)
        {
            List<KnowledgeSubjectRelation> result = new List<KnowledgeSubjectRelation>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (KnowledgeContextKey candidate in KnowledgeContextRegistry.Chain(filter.context, filter.fallback))
            {
                IEnumerable<KnowledgeSubjectRelation> values = KnowledgeRelationService.Query(schema.id, subjectId, true, true, candidate);
                if (candidate.IsEmpty) values = values.Where(item => item.context.IsEmpty);
                foreach (KnowledgeSubjectRelation value in values)
                {
                    if (!filter.developerMode && !value.revealed) continue;
                    string key = value.domainId + "\n" + value.fromSubjectId + "\n" + value.toDomainId + "\n" +
                        value.toSubjectId + "\n" + value.relationTypeId + "\n" + value.context;
                    if (seen.Add(key)) result.Add(value);
                }
            }
            return result;
        }

        private static bool MatchesSearch(KnowledgeBrowserRow row, string search, bool developerMode)
        {
            if (row.displayLabel.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                row.displayDescription.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return developerMode && row.subject.id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }

    public interface IKnowledgeDomainUiV3
    {
        string DomainId { get; }
        IEnumerable<string> ListBadges(KnowledgeBrowserRow row, Pawn pawn, KnowledgeScope scope);
        IEnumerable<string> ListColumns(KnowledgeBrowserRow row, Pawn pawn, KnowledgeScope scope);
        void DrawDetailPanels(Rect rect, KnowledgeBrowserRow row, Pawn pawn, KnowledgeScope scope);
    }

    public static class KnowledgeV3Ui
    {
        private static readonly Dictionary<string, IKnowledgeDomainUiV3> Providers = new Dictionary<string, IKnowledgeDomainUiV3>(StringComparer.Ordinal);

        public static bool Register(IKnowledgeDomainUiV3 provider, bool replace = false)
        {
            if (provider == null || provider.DomainId.NullOrEmpty() || Providers.ContainsKey(provider.DomainId) && !replace) return false;
            Providers[provider.DomainId] = provider;
            return true;
        }

        public static bool Unregister(string domainId) => !domainId.NullOrEmpty() && Providers.Remove(domainId);
        public static IKnowledgeDomainUiV3 Provider(string domainId) => domainId != null && Providers.TryGetValue(domainId, out IKnowledgeDomainUiV3 value) ? value : null;

        public static bool HasDomains => KnowledgeRegistry.SchemasSnapshot.Any();

        public static void OpenColony()
        {
            if (!HasDomains || Find.WindowStack == null) return;
            Find.WindowStack.Add(new Window_KnowledgeBrowser(null, null, null, KnowledgeContextKey.Empty, KnowledgeScope.Colony));
        }

        public static void Open(string domainId, Pawn pawn = null, string subjectId = null, KnowledgeContextKey context = default(KnowledgeContextKey))
        {
            if (Find.WindowStack != null) Find.WindowStack.Add(new Window_KnowledgeBrowser(domainId, pawn, subjectId, context));
        }
    }
}
