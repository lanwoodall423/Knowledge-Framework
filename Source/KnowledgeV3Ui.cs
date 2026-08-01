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
        public KnowledgeBrowserSort sort = KnowledgeBrowserSort.Label;
    }

    public sealed class KnowledgeBrowserRow
    {
        public readonly KnowledgeSubjectSnapshot subject;
        public readonly KnowledgeSubjectSnapshotV2 state;
        public readonly IReadOnlyList<KnowledgeFacetSchema> applicableFacets;
        public readonly float completeness;
        public readonly float confidence;
        public readonly string lastStage;
        public readonly IReadOnlyList<KnowledgeMilestoneState> milestones;
        public readonly IReadOnlyList<KnowledgeSubjectRelation> relations;
        public readonly IReadOnlyList<string> badges;

        internal KnowledgeBrowserRow(KnowledgeSubjectSnapshot subject, KnowledgeSubjectSnapshotV2 state,
            IEnumerable<KnowledgeFacetSchema> applicableFacets, float completeness, float confidence,
            IEnumerable<KnowledgeMilestoneState> milestones, IEnumerable<KnowledgeSubjectRelation> relations, IEnumerable<string> badges)
        {
            this.subject = subject;
            this.state = state;
            this.applicableFacets = new ReadOnlyCollection<KnowledgeFacetSchema>((applicableFacets ?? Enumerable.Empty<KnowledgeFacetSchema>()).ToList());
            this.completeness = completeness;
            this.confidence = confidence;
            lastStage = state.stageId;
            this.milestones = new ReadOnlyCollection<KnowledgeMilestoneState>((milestones ?? Enumerable.Empty<KnowledgeMilestoneState>()).ToList());
            this.relations = new ReadOnlyCollection<KnowledgeSubjectRelation>((relations ?? Enumerable.Empty<KnowledgeSubjectRelation>()).ToList());
            this.badges = new ReadOnlyCollection<string>((badges ?? Enumerable.Empty<string>()).ToList());
        }
    }

    public static class KnowledgeBrowserModels
    {
        public static IReadOnlyList<KnowledgeBrowserRow> Build(KnowledgeBrowserFilter filter)
        {
            if (filter == null || filter.domainId.NullOrEmpty()) return Array.Empty<KnowledgeBrowserRow>();
            KnowledgeSchema schema = KnowledgeRegistry.Schema(filter.domainId);
            if (schema == null) return Array.Empty<KnowledgeBrowserRow>();
            string search = filter.search?.Trim();
            List<KnowledgeBrowserRow> result = new List<KnowledgeBrowserRow>();
            foreach (KnowledgeSubjectSnapshot subject in KnowledgeRegistry.Subjects(schema.id))
            {
                if (subject.state == KnowledgeSubjectState.Archived && !filter.includeArchived || subject.state == KnowledgeSubjectState.Hidden && !filter.includeHidden ||
                    subject.state == KnowledgeSubjectState.MissingContent && !filter.includeMissingContent) continue;
                if (!filter.categoryId.NullOrEmpty() && !subject.categoryIds.Contains(filter.categoryId)) continue;
                if (!search.NullOrEmpty() && subject.label.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 && subject.id.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                IReadOnlyList<KnowledgeFacetSchema> facets = KnowledgeRegistry.ApplicableFacets(schema.id, subject.id);
                if (!filter.facetId.NullOrEmpty() && !facets.Any(item => item.id == filter.facetId)) continue;
                KnowledgeSubjectSnapshotV2 state = KnowledgeQuery.Subject(schema.id, subject.id, filter.pawn, filter.scope);
                KnowledgeFacetSnapshotV2[] values = facets.Select(item => KnowledgeQuery.Facet(schema.id, subject.id, item.id, filter.pawn, filter.scope,
                    true, false, filter.context, KnowledgeContextFallbackMode.ParentThenGlobal)).ToArray();
                float completeness = values.Length == 0 ? 0f : values.Average(item => item.completeness);
                float confidence = values.Length == 0 ? 0f : values.Max(item => item.confidence);
                if (!filter.includeUnknown && state.stageId.NullOrEmpty() && completeness <= 0f) continue;
                result.Add(new KnowledgeBrowserRow(subject, state, facets, completeness, confidence,
                    KnowledgeMilestoneService.States(schema.id, subject.id, filter.pawn, filter.context),
                    KnowledgeRelationService.Query(schema.id, subject.id, true, true, filter.context),
                    subject.state == KnowledgeSubjectState.Archived ? new[] { "archived" } : subject.state == KnowledgeSubjectState.MissingContent ? new[] { "missing" } : null));
            }
            switch (filter.sort)
            {
                case KnowledgeBrowserSort.Stage: return result.OrderByDescending(item => schema.Stage(item.state.stageId)?.order ?? int.MinValue).ThenBy(item => item.subject.label).ToList();
                case KnowledgeBrowserSort.Confidence: return result.OrderByDescending(item => item.confidence).ThenBy(item => item.subject.label).ToList();
                case KnowledgeBrowserSort.Completeness: return result.OrderByDescending(item => item.completeness).ThenBy(item => item.subject.label).ToList();
                default: return result.OrderBy(item => item.subject.label).ThenBy(item => item.subject.id).ToList();
            }
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

        public static void Open(string domainId, Pawn pawn = null, string subjectId = null, KnowledgeContextKey context = default(KnowledgeContextKey))
        {
            if (Find.WindowStack != null) Find.WindowStack.Add(new Window_KnowledgeBrowser(domainId, pawn, subjectId));
        }
    }
}
