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

    public interface IKnowledgeDomainUiV2Contextual
    {
        IEnumerable<FloatMenuOption> SubjectActions(KnowledgeSubjectSnapshot subject, Pawn pawn, KnowledgeScope scope,
            KnowledgeContextKey context);
        void DrawSubjectDetails(Rect rect, KnowledgeSubjectSnapshot subject, Pawn pawn, KnowledgeScope scope,
            KnowledgeContextKey context);
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

        public static void Open(string domainId = null, Pawn pawn = null, string subjectId = null,
            KnowledgeContextKey context = default(KnowledgeContextKey))
        {
            if (Find.WindowStack == null) return;
            Find.WindowStack.Add(new Window_KnowledgeBrowser(domainId, pawn, subjectId, context));
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
        private KnowledgeContextKey context;
        private KnowledgeScope scope;
        private string search = string.Empty;
        private Vector2 subjectScroll;
        private Vector2 facetScroll;
        private int modelRevision = -1;
        private int modelKnowledgeRevision = -1;
        private int modelPawnId;
        private KnowledgeScope modelScope;
        private KnowledgeContextKey modelContext;
        private string modelDomain;
        private List<KnowledgeSubjectSnapshot> subjects = new List<KnowledgeSubjectSnapshot>();
        private Dictionary<string, KnowledgeRevealResult> subjectPresentations = new Dictionary<string, KnowledgeRevealResult>(StringComparer.Ordinal);
        private KnowledgeBrowserRow detailModel;
        private int detailKnowledgeRevision = -1;
        private int detailUiRevision = -1;
        private int detailPawnId;
        private KnowledgeScope detailScope;
        private string detailDomain;
        private string detailSubject;
        private KnowledgeContextKey detailContext;
        private int contextOptionsKnowledgeRevision = -1;
        private int contextOptionsUiRevision = -1;
        private int contextOptionsPawnId;
        private KnowledgeScope contextOptionsScope;
        private string contextOptionsDomain;
        private string contextOptionsSubject;
        private List<KnowledgeContextKey> contextOptions = new List<KnowledgeContextKey>();
        private bool allowExplicitContext;
        private bool contextOptionsAllowExplicit;

        public override Vector2 InitialSize => new Vector2(Mathf.Min(1100f, UI.screenWidth * 0.92f), Mathf.Min(760f, UI.screenHeight * 0.9f));

        internal KnowledgeContextKey RequestedContext => context;
        internal KnowledgeScope SelectedScope => scope;
        internal IReadOnlyList<KnowledgeContextKey> ContextOptionsForVerification(KnowledgeSchema schema) => ContextOptions(schema);

        public Window_KnowledgeBrowser(string domainId, Pawn pawn, string subjectId,
            KnowledgeContextKey context = default(KnowledgeContextKey), KnowledgeScope scope = KnowledgeScope.Personal)
        {
            this.domainId = domainId;
            this.pawn = pawn;
            this.subjectId = subjectId;
            this.context = context;
            this.scope = scope;
            allowExplicitContext = !context.IsEmpty && !context.IsPartial;
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
                    context = KnowledgeContextKey.Empty;
                    allowExplicitContext = false;
                    modelRevision = -1;
                    modelKnowledgeRevision = -1;
                    modelDomain = null;
                    detailModel = null;
                })).ToList();
                Find.WindowStack.Add(new FloatMenu(options));
            }
            float scopeX = header.xMax - 230f;
            if (Widgets.ButtonText(new Rect(scopeX, header.y, 108f, 34f), "KnowledgeFramework_Colonist".Translate(), active: scope != KnowledgeScope.Personal))
            {
                scope = KnowledgeScope.Personal;
                allowExplicitContext = false;
            }
            if (Widgets.ButtonText(new Rect(scopeX + 116f, header.y, 108f, 34f), "KnowledgeFramework_Colony".Translate(), active: scope != KnowledgeScope.Colony))
            {
                scope = KnowledgeScope.Colony;
                allowExplicitContext = false;
            }
            if (scope == KnowledgeScope.Personal && pawn == null) pawn = Find.Selector?.SingleSelectedThing as Pawn;

            IReadOnlyList<KnowledgeContextKey> availableContexts = ContextOptions(schema);
            bool contextualDomain = schema.stages.Any(item => item.contextSensitive) || availableContexts.Count > 1;
            if (!availableContexts.Contains(context))
            {
                context = KnowledgeContextKey.Empty;
                allowExplicitContext = false;
            }
            if (contextualDomain)
            {
                Rect contextRect = new Rect(header.x, header.yMax + 4f, header.width, 32f);
                if (Widgets.ButtonText(contextRect, KnowledgeBrowserLabels.ContextSelection(context, schema.id, subjectId, pawn, scope)))
                {
                    List<FloatMenuOption> options = availableContexts.Select(value => new FloatMenuOption(
                        KnowledgeBrowserLabels.ContextSelection(value, schema.id, subjectId, pawn, scope), () =>
                        {
                            context = value;
                            allowExplicitContext = true;
                            detailModel = null;
                            modelRevision = -1;
                        })).ToList();
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            }

            float bodyTop = contextualDomain ? header.yMax + 44f : header.yMax + 8f;
            Rect body = new Rect(rect.x, bodyTop, rect.width, Mathf.Max(0f, rect.height - (bodyTop - rect.y)));
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
            int knowledgeRevision = KnowledgeQuery.Revision;
            int uiRevision = KnowledgeUiCache.Revision;
            int pawnId = pawn?.thingIDNumber ?? 0;
            if (modelRevision != uiRevision || modelKnowledgeRevision != knowledgeRevision || modelDomain != schema.id ||
                modelPawnId != pawnId || modelScope != scope || !modelContext.Equals(context))
            {
                subjects = KnowledgeRegistry.Subjects(schema.id).ToList();
                subjectPresentations = subjects.ToDictionary(item => item.id, item => KnowledgeDiscovery.Present(schema.id, item.id, null,
                    pawn, scope, context, KnowledgeContextFallbackMode.ParentThenGlobal), StringComparer.Ordinal);
                modelRevision = uiRevision;
                modelKnowledgeRevision = knowledgeRevision;
                modelDomain = schema.id;
                modelPawnId = pawnId;
                modelScope = scope;
                modelContext = context;
            }
            List<KnowledgeSubjectSnapshot> filtered = subjects.Where(item => SubjectVisibleInList(item, search)).ToList();
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
                    KnowledgeRevealResult presentation = subjectPresentations[subject.id];
                    Rect row = new Rect(0f, i * 42f, view.width, 38f);
                    if (subject.id == subjectId) Widgets.DrawHighlightSelected(row); else Widgets.DrawHighlightIfMouseover(row);
                    Widgets.Label(new Rect(row.x + 5f, row.y + 3f, row.width - 10f, 22f),
                        KnowledgeBrowserLabels.SubjectLabel(subject, presentation));
                    GUI.color = Color.gray;
                    Widgets.Label(new Rect(row.x + 5f, row.y + 21f, row.width - 10f, 17f),
                        KnowledgeBrowserLabels.Stage(schema, presentation.stageId));
                    GUI.color = Color.white;
                if (Widgets.ButtonInvisible(row))
                {
                    if (subjectId != subject.id) allowExplicitContext = false;
                    subjectId = subject.id;
                }
                }
            }
            finally { Widgets.EndScrollView(); }
            if (filtered.Count == 0) Widgets.Label(outer.ContractedBy(8f), "KnowledgeFramework_NoMatches".Translate());
        }

        private bool SubjectVisibleInList(KnowledgeSubjectSnapshot subject, string search)
        {
            if (subject == null) return false;
            if (subject.state == KnowledgeSubjectState.Archived || subject.state == KnowledgeSubjectState.Retired ||
                subject.state == KnowledgeSubjectState.Hidden || subject.state == KnowledgeSubjectState.MissingContent) return false;
            if (!subjectPresentations.TryGetValue(subject.id, out KnowledgeRevealResult presentation)) return false;
            if (!presentation.identified) return false;
            if (search.NullOrEmpty()) return true;
            return KnowledgeBrowserLabels.SubjectLabel(subject, presentation).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                KnowledgeBrowserLabels.SubjectDescription(subject, presentation).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawDetails(Rect rect, KnowledgeSchema schema)
        {
            Widgets.DrawMenuSection(rect);
            Rect inner = rect.ContractedBy(12f);
            KnowledgeBrowserRow browserRow = DetailModel(schema);
            if (browserRow == null) { Widgets.Label(inner, "KnowledgeFramework_SelectSubject".Translate()); return; }
            KnowledgeSubjectSnapshot subject = browserRow.subject;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inner.x, inner.y, inner.width, 34f), browserRow.displayLabel);
            IKnowledgeDomainUiV2 provider = KnowledgeV2Ui.Provider(schema.id);
            IKnowledgeDomainUiV2Contextual contextualProvider = provider as IKnowledgeDomainUiV2Contextual;
            List<FloatMenuOption> actions = (contextualProvider == null ? provider?.SubjectActions(subject, pawn, scope) :
                contextualProvider.SubjectActions(subject, pawn, scope, context))?.Where(item => item != null).ToList();
            if (actions != null && actions.Count > 0 && Widgets.ButtonText(new Rect(inner.xMax - 92f, inner.y, 92f, 30f), "KnowledgeFramework_Actions".Translate()))
                Find.WindowStack.Add(new FloatMenu(actions));
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(inner.x, inner.y + 38f, inner.width, 24f), "KnowledgeFramework_StageValue".Translate(
                KnowledgeBrowserLabels.Stage(schema, browserRow.currentStageId)));
            string contextText = browserRow.usedContextFallback
                ? "KnowledgeFramework_ContextFallback".Translate(
                    KnowledgeBrowserLabels.ContextSelection(browserRow.requestedContext, schema.id, subject.id, pawn, scope),
                    KnowledgeBrowserLabels.ContextSelection(browserRow.resolvedContext, schema.id, subject.id, pawn, scope))
                : "KnowledgeFramework_ContextValue".Translate(
                    KnowledgeBrowserLabels.ContextSelection(browserRow.requestedContext, schema.id, subject.id, pawn, scope));
            GUI.color = Color.gray;
            Widgets.Label(new Rect(inner.x, inner.y + 62f, inner.width, 22f), contextText);
            GUI.color = Color.white;
            float y = inner.y + 88f;
            IKnowledgeDomainUiV3 v3Provider = KnowledgeV3Ui.Provider(schema.id);
            if (v3Provider != null)
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
            IReadOnlyList<KnowledgeFacetSchema> applicableFacets = browserRow.applicableFacets;
            List<KnowledgeSubjectRelation> relationships = browserRow.relations.ToList();
            List<KnowledgeInsightProgress> insights = browserRow.insights.ToList();
            Rect outer = new Rect(inner.x, y, inner.width, inner.yMax - y);
            List<KnowledgeClaimSnapshot> claims = browserRow.claims.ToList();
            List<KnowledgeMilestoneState> milestones = browserRow.milestones.ToList();
            float extraHeight = relationships.Count * 38f + insights.Count * 38f + claims.Count * 34f + milestones.Count * 42f +
                (browserRow.unmetRequirementCount > 0 ? 30f : 0f) + 80f;
            Rect view = new Rect(0f, 0f, outer.width - 16f, Math.Max(outer.height, applicableFacets.Count * 72f + extraHeight));
            Widgets.BeginScrollView(outer, ref facetScroll, view);
            try
            {
                for (int i = 0; i < applicableFacets.Count; i++)
                {
                    KnowledgeFacetSchema facet = applicableFacets[i];
                    if (!browserRow.facetValues.TryGetValue(facet.id, out KnowledgeFacetSnapshotV2 value)) continue;
                    Rect row = new Rect(0f, i * 72f, view.width, 66f);
                    Widgets.Label(new Rect(row.x, row.y, row.width * 0.5f, 24f), KnowledgeBrowserLabels.Facet(facet));
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
                        KnowledgeSubjectRelation relationship = relationships[i];
                        bool outgoing = relationship.fromSubjectId == subject.id && relationship.domainId == schema.id;
                        string relatedDomain = outgoing ? relationship.toDomainId : relationship.domainId;
                        string relatedSubject = outgoing ? relationship.toSubjectId : relationship.fromSubjectId;
                        string relatedLabel = KnowledgeBrowserLabels.Subject(relatedDomain, relatedSubject, pawn, scope,
                            relationship.context);
                        Widgets.Label(new Rect(4f, extraY + i * 38f, view.width, 32f),
                            "KnowledgeFramework_RelationshipDetail".Translate(KnowledgeBrowserLabels.RelationType(relationship.relationTypeId),
                                relatedLabel, relationship.confidence.ToStringPercent()));
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
                            insight.requirementsMet ? "KnowledgeFramework_InsightReady".Translate(KnowledgeBrowserLabels.Insight(schema, insight.insightId)) :
                            "KnowledgeFramework_InsightProgress".Translate(KnowledgeBrowserLabels.Insight(schema, insight.insightId), insight.unmetRequirements.Count));
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
                        Widgets.Label(new Rect(4f, extraY, view.width, 28f), KnowledgeBrowserLabels.Claim(schema, claim.claimId) + ": " + value +
                            " (" + claim.effectiveConfidence.ToStringPercent() + ")");
                        extraY += 34f;
                    }
                }
                if (milestones.Count > 0)
                {
                    Widgets.Label(new Rect(0f, extraY, view.width, 24f), "KnowledgeFramework_Milestones".Translate());
                    extraY += 26f;
                    foreach (KnowledgeMilestoneState milestone in milestones)
                    {
                        Widgets.Label(new Rect(4f, extraY, view.width, 36f),
                            KnowledgeBrowserLabels.Track(schema, milestone.trackId) + ": " +
                            KnowledgeBrowserLabels.Milestone(schema, milestone.trackId, milestone.milestoneId) + " " +
                            (milestone.completed ? "100%" : milestone.progress.ToStringPercent()));
                        extraY += 42f;
                    }
                }
                if (browserRow.unmetRequirementCount > 0)
                {
                    Widgets.Label(new Rect(0f, extraY, view.width, 24f), "KnowledgeFramework_RequirementsUnmet".Translate(
                        browserRow.unmetRequirementCount));
                    extraY += 30f;
                }
                if (contextualProvider != null)
                    contextualProvider.DrawSubjectDetails(new Rect(0f, extraY, view.width, 80f), subject, pawn, scope, context);
                else
                    provider?.DrawSubjectDetails(new Rect(0f, extraY, view.width, 80f), subject, pawn, scope);
            }
            finally { Widgets.EndScrollView(); }
        }

        private KnowledgeBrowserRow DetailModel(KnowledgeSchema schema)
        {
            int knowledgeRevision = KnowledgeQuery.Revision;
            int uiRevision = KnowledgeUiCache.Revision;
            int pawnId = pawn?.thingIDNumber ?? 0;
            if (detailModel == null || detailKnowledgeRevision != knowledgeRevision || detailUiRevision != uiRevision ||
                detailPawnId != pawnId || detailScope != scope || detailDomain != schema.id || detailSubject != subjectId ||
                !detailContext.Equals(context))
            {
                detailModel = KnowledgeBrowserModels.BuildSubject(new KnowledgeBrowserFilter
                {
                    domainId = schema.id,
                    pawn = pawn,
                    scope = scope,
                    context = context,
                    fallback = KnowledgeContextFallbackMode.ParentThenGlobal
                }, subjectId);
                detailKnowledgeRevision = knowledgeRevision;
                detailUiRevision = uiRevision;
                detailPawnId = pawnId;
                detailScope = scope;
                detailDomain = schema.id;
                detailSubject = subjectId;
                detailContext = context;
            }
            return detailModel;
        }

        private IReadOnlyList<KnowledgeContextKey> ContextOptions(KnowledgeSchema schema)
        {
            int knowledgeRevision = KnowledgeQuery.Revision;
            int uiRevision = KnowledgeUiCache.Revision;
            int pawnId = pawn?.thingIDNumber ?? 0;
            if (contextOptionsKnowledgeRevision != knowledgeRevision || contextOptionsUiRevision != uiRevision ||
                contextOptionsPawnId != pawnId || contextOptionsScope != scope || contextOptionsDomain != schema.id ||
                contextOptionsSubject != subjectId || contextOptionsAllowExplicit != allowExplicitContext)
            {
                contextOptions = KnowledgeBrowserModels.ContextOptions(new KnowledgeBrowserFilter
                {
                    domainId = schema.id,
                    pawn = pawn,
                    scope = scope,
                    context = context
                }, subjectId, allowExplicitContext).ToList();
                contextOptionsKnowledgeRevision = knowledgeRevision;
                contextOptionsUiRevision = uiRevision;
                contextOptionsPawnId = pawnId;
                contextOptionsScope = scope;
                contextOptionsDomain = schema.id;
                contextOptionsSubject = subjectId;
                contextOptionsAllowExplicit = allowExplicitContext;
            }
            return contextOptions;
        }
    }
}
