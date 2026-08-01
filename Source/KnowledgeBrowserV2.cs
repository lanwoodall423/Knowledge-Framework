using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace KnowledgeFramework
{
    public interface IKnowledgeDomainUiV2
    {
        string DomainId { get; }
        int Priority { get; }
        IEnumerable<FloatMenuOption> SubjectActions(KnowledgeSubjectSnapshot subject, Pawn pawn, KnowledgeScope scope);
        void DrawSubjectDetails(Rect rect, KnowledgeSubjectSnapshot subject, Pawn pawn, KnowledgeScope scope);
    }

    public static class KnowledgeV2Ui
    {
        private static readonly Dictionary<string, IKnowledgeDomainUiV2> Providers = new Dictionary<string, IKnowledgeDomainUiV2>(StringComparer.Ordinal);

        public static bool Register(IKnowledgeDomainUiV2 provider, bool replace = false)
        {
            if (provider == null || provider.DomainId.NullOrEmpty() || KnowledgeRegistry.Schema(provider.DomainId) == null ||
                Providers.ContainsKey(provider.DomainId) && !replace) return false;
            Providers[provider.DomainId] = provider;
            return true;
        }

        public static bool Unregister(string domainId) => !domainId.NullOrEmpty() && Providers.Remove(domainId);

        public static void Open(string domainId = null, Pawn pawn = null, string subjectId = null)
        {
            if (Find.WindowStack == null) return;
            Find.WindowStack.Add(new Window_KnowledgeBrowser(domainId, pawn, subjectId));
        }

        internal static IKnowledgeDomainUiV2 Provider(string domainId) =>
            !domainId.NullOrEmpty() && Providers.TryGetValue(domainId, out IKnowledgeDomainUiV2 value) ? value : null;

        internal static void EnsureBioProvider(KnowledgeSchema schema)
        {
            KnowledgeProviderRegistry.Register(schema.id, schema.sortOrder, pawn =>
            {
                KnowledgeExpertiseSnapshotV2 expertise = schema.expertiseTracks.Count == 0 ? null : KnowledgeQuery.Expertise(schema.id, pawn, schema.expertiseTracks[0].id);
                int known = KnowledgeQuery.PersonalFacets(schema.id, pawn).Count(item => item.amount > 0f);
                if (known == 0 && (expertise?.amount ?? 0f) <= 0f) return null;
                return new KnowledgeEntry
                {
                    label = schema.label,
                    rank = expertise?.rank ?? KnowledgeRank.Novice,
                    progress = expertise?.progress ?? 0f,
                    summary = "KnowledgeFramework_KnownFacetCount".Translate(known),
                    tooltip = schema.description,
                    openDetails = () => Open(schema.id, pawn)
                };
            });
        }
    }

    public sealed class Window_KnowledgeBrowser : Window
    {
        private string domainId;
        private Pawn pawn;
        private string subjectId;
        private KnowledgeScope scope;
        private string search = string.Empty;
        private Vector2 subjectScroll;
        private Vector2 facetScroll;
        private int modelRevision = -1;
        private List<KnowledgeSubjectSnapshot> subjects = new List<KnowledgeSubjectSnapshot>();

        public override Vector2 InitialSize => new Vector2(Mathf.Min(1100f, UI.screenWidth * 0.92f), Mathf.Min(760f, UI.screenHeight * 0.9f));

        public Window_KnowledgeBrowser(string domainId, Pawn pawn, string subjectId)
        {
            this.domainId = domainId;
            this.pawn = pawn;
            this.subjectId = subjectId;
            doCloseX = true;
            doCloseButton = false;
            absorbInputAroundWindow = false;
            resizeable = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Color oldColor = GUI.color;
            TextAnchor oldAnchor = Text.Anchor;
            GameFont oldFont = Text.Font;
            bool oldWrap = Text.WordWrap;
            try { DrawContents(inRect); }
            catch (Exception exception) { KnowledgeLog.ErrorOnce("ui:browser", "The knowledge browser failed to draw.", exception); }
            finally
            {
                GUI.color = oldColor;
                Text.Anchor = oldAnchor;
                Text.Font = oldFont;
                Text.WordWrap = oldWrap;
            }
        }

        private void DrawContents(Rect rect)
        {
            IReadOnlyCollection<KnowledgeSchema> schemas = KnowledgeRegistry.SchemasSnapshot;
            KnowledgeSchema schema = KnowledgeRegistry.Schema(domainId) ?? schemas.FirstOrDefault();
            if (schema == null) { Widgets.Label(rect, "KnowledgeFramework_NoDomains".Translate()); return; }
            domainId = schema.id;
            Rect header = new Rect(rect.x, rect.y, rect.width, 38f);
            if (Widgets.ButtonText(new Rect(header.x, header.y, Mathf.Min(260f, header.width * 0.4f), 34f), schema.label))
            {
                List<FloatMenuOption> options = schemas.Select(item => new FloatMenuOption(item.label, () =>
                {
                    domainId = item.id;
                    subjectId = null;
                    modelRevision = -1;
                })).ToList();
                Find.WindowStack.Add(new FloatMenu(options));
            }
            float scopeX = header.xMax - 230f;
            if (Widgets.ButtonText(new Rect(scopeX, header.y, 108f, 34f), "KnowledgeFramework_Colonist".Translate(), active: scope != KnowledgeScope.Personal))
                scope = KnowledgeScope.Personal;
            if (Widgets.ButtonText(new Rect(scopeX + 116f, header.y, 108f, 34f), "KnowledgeFramework_Colony".Translate(), active: scope != KnowledgeScope.Colony))
                scope = KnowledgeScope.Colony;
            if (scope == KnowledgeScope.Personal && pawn == null) pawn = Find.Selector?.SingleSelectedThing as Pawn;

            Rect body = new Rect(rect.x, header.yMax + 8f, rect.width, rect.height - header.height - 8f);
            float listWidth = Mathf.Clamp(body.width * 0.38f, 230f, 390f);
            bool stacked = body.width < 560f;
            if (stacked) listWidth = body.width;
            float listHeight = stacked ? Mathf.Clamp(body.height * 0.42f, 170f, 300f) : body.height;
            Rect listRect = new Rect(body.x, body.y, listWidth, listHeight);
            Rect detailRect = stacked
                ? new Rect(body.x, listRect.yMax + 10f, body.width, Mathf.Max(0f, body.yMax - listRect.yMax - 10f))
                : new Rect(listRect.xMax + 10f, body.y, body.width - listRect.width - 10f, body.height);
            DrawSubjectList(listRect, schema);
            if (detailRect.width > 0f) DrawDetails(detailRect, schema);
        }

        private void DrawSubjectList(Rect rect, KnowledgeSchema schema)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(8f);
            search = Widgets.TextField(new Rect(inner.x, inner.y, inner.width, 30f), search ?? string.Empty);
            if (modelRevision != KnowledgeUiCache.Revision)
            {
                subjects = KnowledgeRegistry.Subjects(schema.id).ToList();
                modelRevision = KnowledgeUiCache.Revision;
            }
            List<KnowledgeSubjectSnapshot> filtered = subjects.Where(item => search.NullOrEmpty() ||
                item.label.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            Rect outer = new Rect(inner.x, inner.y + 38f, inner.width, inner.height - 38f);
            Rect view = new Rect(0f, 0f, outer.width - 16f, Math.Max(outer.height, filtered.Count * 42f));
            Widgets.BeginScrollView(outer, ref subjectScroll, view);
            try
            {
                int first = Mathf.Max(0, Mathf.FloorToInt(subjectScroll.y / 42f) - 1);
                int last = Mathf.Min(filtered.Count, Mathf.CeilToInt((subjectScroll.y + outer.height) / 42f) + 1);
                for (int i = first; i < last; i++)
                {
                    KnowledgeSubjectSnapshot subject = filtered[i];
                    KnowledgeSubjectSnapshotV2 state = KnowledgeQuery.Subject(schema.id, subject.id, pawn, scope);
                    bool identified = !state.stageId.NullOrEmpty() || schema.stages.Count == 0 || KnowledgeQuery.Facet(schema.id, subject.id,
                        null, pawn, scope).amount > 0f;
                    Rect row = new Rect(0f, i * 42f, view.width, 38f);
                    if (subject.id == subjectId) Widgets.DrawHighlightSelected(row); else Widgets.DrawHighlightIfMouseover(row);
                    Widgets.Label(new Rect(row.x + 5f, row.y + 3f, row.width - 10f, 22f), identified ? subject.label :
                        (subject.unidentifiedLabel.NullOrEmpty() ? "KnowledgeFramework_Unidentified".Translate() : subject.unidentifiedLabel));
                    GUI.color = Color.gray;
                    Widgets.Label(new Rect(row.x + 5f, row.y + 21f, row.width - 10f, 17f), state.stageId ?? "KnowledgeFramework_Unknown".Translate());
                    GUI.color = Color.white;
                    if (Widgets.ButtonInvisible(row)) subjectId = subject.id;
                }
            }
            finally { Widgets.EndScrollView(); }
            if (filtered.Count == 0) Widgets.Label(outer.ContractedBy(8f), "KnowledgeFramework_NoMatches".Translate());
        }

        private void DrawDetails(Rect rect, KnowledgeSchema schema)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(12f);
            KnowledgeSubjectSnapshot subject = KnowledgeRegistry.ResolveSubject(schema.id, subjectId);
            if (subject == null) { Widgets.Label(inner, "KnowledgeFramework_SelectSubject".Translate()); return; }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 34f), subject.label);
            IKnowledgeDomainUiV2 provider = KnowledgeV2Ui.Provider(schema.id);
            List<FloatMenuOption> actions = provider?.SubjectActions(subject, pawn, scope)?.Where(item => item != null).ToList();
            if (actions != null && actions.Count > 0 && Widgets.ButtonText(new Rect(inner.xMax - 92f, inner.y, 92f, 30f), "KnowledgeFramework_Actions".Translate()))
                Find.WindowStack.Add(new FloatMenu(actions));
            Text.Font = GameFont.Small;
            KnowledgeSubjectSnapshotV2 state = KnowledgeQuery.Subject(schema.id, subject.id, pawn, scope);
            Widgets.Label(new Rect(inner.x, inner.y + 38f, inner.width, 24f), "KnowledgeFramework_StageValue".Translate(state.stageId ?? "KnowledgeFramework_Unknown".Translate()));
            float y = inner.y + 68f;
            IKnowledgeDomainUiV3 v3Provider = KnowledgeV3Ui.Provider(schema.id);
            if (v3Provider != null)
            {
                KnowledgeBrowserRow browserRow = KnowledgeBrowserModels.Build(new KnowledgeBrowserFilter
                {
                    domainId = schema.id,
                    pawn = pawn,
                    scope = scope,
                    search = subject.id,
                    includeUnknown = true,
                    includeArchived = true,
                    includeHidden = true,
                    includeMissingContent = true
                }).FirstOrDefault();
                if (browserRow != null)
                {
                    List<string> badges = v3Provider.ListBadges(browserRow, pawn, scope)?.Where(value => !value.NullOrEmpty()).ToList() ?? new List<string>();
                    List<string> columns = v3Provider.ListColumns(browserRow, pawn, scope)?.Where(value => !value.NullOrEmpty()).ToList() ?? new List<string>();
                    Rect providerRect = new Rect(inner.x, y, inner.width, 72f);
                    v3Provider.DrawDetailPanels(providerRect, browserRow, pawn, scope);
                    if (badges.Count > 0 || columns.Count > 0)
                    {
                        GUI.color = Color.gray;
                        Widgets.Label(new Rect(providerRect.x + 8f, providerRect.y + 44f, providerRect.width - 16f, 22f),
                            string.Join("  |  ", badges.Concat(columns)));
                        GUI.color = Color.white;
                    }
                    y += 82f;
                }
            }
            IReadOnlyList<KnowledgeFacetSchema> applicableFacets = KnowledgeRegistry.ApplicableFacets(schema.id, subject.id);
            List<KnowledgeRelationshipSnapshot> relationships = applicableFacets.SelectMany(facet => KnowledgeQuery.Relationships(
                schema.id, subject.id, facet.id, pawn, scope)).ToList();
            List<KnowledgeInsightProgress> insights = schema.insights.Select(insight => KnowledgeInsightService.Progress(
                insight.defName, schema.id, subject.id, pawn, scope)).ToList();
            Rect outer = new Rect(inner.x, y, inner.width, inner.yMax - y);
            List<KnowledgeClaimSnapshot> claims = applicableFacets.SelectMany(facet => KnowledgeClaimService.ForSubject(schema.id, subject.id, facet.id, pawn, scope)).ToList();
            List<KnowledgeMilestoneState> milestones = KnowledgeMilestoneService.States(schema.id, subject.id, pawn).ToList();
            float extraHeight = relationships.Count * 38f + insights.Count * 38f + claims.Count * 34f + milestones.Count * 30f + 80f;
            Rect view = new Rect(0f, 0f, outer.width - 16f, Math.Max(outer.height, applicableFacets.Count * 72f + extraHeight));
            Widgets.BeginScrollView(outer, ref facetScroll, view);
            try
            {
                for (int i = 0; i < applicableFacets.Count; i++)
                {
                    KnowledgeFacetSchema facet = applicableFacets[i];
                    KnowledgeFacetSnapshotV2 value = KnowledgeQuery.Facet(schema.id, subject.id, facet.id, pawn, scope);
                    Rect row = new Rect(0f, i * 72f, view.width, 66f);
                    Widgets.Label(new Rect(row.x, row.y, row.width * 0.5f, 24f), facet.label);
                    Widgets.FillableBar(new Rect(row.x, row.y + 28f, row.width, 22f), value.completeness);
                    Text.Anchor = TextAnchor.MiddleCenter;
                    Widgets.Label(new Rect(row.x, row.y + 28f, row.width, 22f), "KnowledgeFramework_KnowledgeAndConfidence".Translate(
                        value.completeness.ToStringPercent(), value.confidence.ToStringPercent()));
                    Text.Anchor = TextAnchor.UpperLeft;
                    if (value.provisional) TooltipHandler.TipRegion(row, "KnowledgeFramework_ProvisionalKnowledge".Translate());
                }
                float extraY = applicableFacets.Count * 72f;
                if (relationships.Count > 0)
                {
                    Widgets.Label(new Rect(0f, extraY, view.width, 24f), "KnowledgeFramework_Relationships".Translate());
                    extraY += 26f;
                    for (int i = 0; i < relationships.Count; i++)
                    {
                        KnowledgeRelationshipSnapshot relationship = relationships[i];
                        Widgets.Label(new Rect(4f, extraY + i * 38f, view.width, 32f),
                            "KnowledgeFramework_RelationshipValue".Translate(relationship.fromSubjectId,
                                relationship.derivedAmount.ToString("0.##"), relationship.derivedConfidence.ToStringPercent()));
                    }
                    extraY += relationships.Count * 38f;
                }
                if (insights.Count > 0)
                {
                    Widgets.Label(new Rect(0f, extraY, view.width, 24f), "KnowledgeFramework_Insights".Translate());
                    extraY += 26f;
                    for (int i = 0; i < insights.Count; i++)
                    {
                        KnowledgeInsightProgress insight = insights[i];
                        Widgets.Label(new Rect(4f, extraY + i * 38f, view.width, 32f),
                            insight.requirementsMet ? "KnowledgeFramework_InsightReady".Translate(insight.insightId) :
                            "KnowledgeFramework_InsightProgress".Translate(insight.insightId, insight.unmetRequirements.Count));
                    }
                    extraY += insights.Count * 38f;
                }
                if (claims.Count > 0)
                {
                    Widgets.Label(new Rect(0f, extraY, view.width, 24f), "KnowledgeFramework_Claims".Translate());
                    extraY += 26f;
                    foreach (KnowledgeClaimSnapshot claim in claims)
                    {
                        string value = claim.value == null ? "KnowledgeFramework_Unknown".Translate() : claim.value.ToString();
                        Widgets.Label(new Rect(4f, extraY, view.width, 28f), claim.claimId + ": " + value + " (" + claim.effectiveConfidence.ToStringPercent() + ")");
                        extraY += 34f;
                    }
                }
                if (milestones.Count > 0)
                {
                    Widgets.Label(new Rect(0f, extraY, view.width, 24f), "KnowledgeFramework_Milestones".Translate());
                    extraY += 26f;
                    foreach (KnowledgeMilestoneState milestone in milestones)
                    {
                        Widgets.Label(new Rect(4f, extraY, view.width, 24f), milestone.milestoneId + ": " + (milestone.completed ? "100%" : milestone.progress.ToStringPercent()));
                        extraY += 30f;
                    }
                }
                provider?.DrawSubjectDetails(new Rect(0f, extraY, view.width, 80f), subject, pawn, scope);
            }
            finally { Widgets.EndScrollView(); }
        }
    }
}
