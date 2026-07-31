using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace KnowledgeFramework
{
    public enum KnowledgeMenuScope
    {
        Colonist,
        Colony
    }

    public sealed class KnowledgeMenuState
    {
        public KnowledgeMenuScope scope;
        public Pawn selectedPawn;
        public string selectedSection;
        public string search = string.Empty;
        public Vector2 pawnScroll;
        public Vector2 subjectScroll;
        internal int cachedRevision = -1;
        internal int cachedPawnId;
        internal KnowledgeMenuScope cachedScope;
        internal KnowledgeMenuModel cachedModel;
    }

    public sealed class KnowledgeMenuRow
    {
        public string label;
        public ThingDef iconDef;
        public KnowledgeRank rank;
        public float progress;
        public string status;
        public string tooltip;
        public string subjectId;
        public string stageId;
        public float confidence = -1f;
        public bool provisional;
        public bool locked;
        public Action select;
        public List<FloatMenuOption> actions;
    }

    public sealed class KnowledgeMenuSection
    {
        public string id;
        public string label;
        public string emptyText;
        public List<KnowledgeMenuRow> rows = new List<KnowledgeMenuRow>();
    }

    public sealed class KnowledgeMenuModel
    {
        public string title;
        public string expertiseLabel = "KnowledgeFramework_Expertise".Translate();
        public KnowledgeRank expertiseRank;
        public float expertiseProgress;
        public List<KnowledgeMenuSection> sections = new List<KnowledgeMenuSection>();
    }

    public static class KnowledgeMenuUI
    {
        private const float Gap = 12f;
        private const float RowHeight = 48f;

        public static void Draw(Rect rect, KnowledgeMenuState state, Func<Pawn, bool, KnowledgeMenuModel> modelFor,
            Func<Pawn, KnowledgeRank> expertiseFor = null)
        {
            if (state == null || modelFor == null) return;
            Color oldColor = GUI.color;
            TextAnchor oldAnchor = Text.Anchor;
            GameFont oldFont = Text.Font;
            bool oldWordWrap = Text.WordWrap;
            try
            {
                List<Pawn> pawns = PawnList();
                if (state.selectedPawn == null || !pawns.Contains(state.selectedPawn)) state.selectedPawn = pawns.FirstOrDefault();
                bool stacked = rect.width < 620f;
                float leftWidth = stacked ? rect.width : Mathf.Clamp(rect.width * 0.32f, 210f, Mathf.Min(380f, rect.width * 0.45f));
                Rect left = stacked ? new Rect(rect.x, rect.y, leftWidth, Mathf.Min(160f, rect.height * 0.3f)) : new Rect(rect.x, rect.y, leftWidth, rect.height);
                Rect right = stacked ? new Rect(rect.x, left.yMax + Gap, rect.width, rect.yMax - left.yMax - Gap) :
                    new Rect(left.xMax + Gap, rect.y, rect.width - left.width - Gap, rect.height);
                Widgets.DrawMenuSection(left);
                Widgets.DrawMenuSection(right);
                DrawNavigation(left.ContractedBy(10f), state, pawns, expertiseFor, stacked);

                bool colony = state.scope == KnowledgeMenuScope.Colony;
                int pawnId = state.selectedPawn?.thingIDNumber ?? 0;
                if (state.cachedModel == null || state.cachedRevision != KnowledgeUiCache.Revision || state.cachedPawnId != pawnId ||
                    state.cachedScope != state.scope)
                {
                    state.cachedModel = modelFor(state.selectedPawn, colony);
                    state.cachedRevision = KnowledgeUiCache.Revision;
                    state.cachedPawnId = pawnId;
                    state.cachedScope = state.scope;
                }
                DrawDetail(right.ContractedBy(14f), state, state.cachedModel, colony);
            }
            catch (Exception exception)
            {
                KnowledgeLog.ErrorOnce("ui:menu", "The shared knowledge menu failed to draw.", exception);
            }
            finally
            {
                GUI.color = oldColor;
                Text.Anchor = oldAnchor;
                Text.Font = oldFont;
                Text.WordWrap = oldWordWrap;
            }
        }

        private static void DrawNavigation(Rect rect, KnowledgeMenuState state, List<Pawn> pawns,
            Func<Pawn, KnowledgeRank> expertiseFor, bool compact)
        {
            float modeWidth = (rect.width - 8f) * 0.5f;
            if (Widgets.ButtonText(new Rect(rect.x, rect.y, modeWidth, 34f), "KnowledgeFramework_Colonist".Translate(),
                    active: state.scope != KnowledgeMenuScope.Colonist))
            {
                state.scope = KnowledgeMenuScope.Colonist;
                state.subjectScroll = Vector2.zero;
            }
            if (Widgets.ButtonText(new Rect(rect.x + modeWidth + 8f, rect.y, modeWidth, 34f), "KnowledgeFramework_Colony".Translate(),
                    active: state.scope != KnowledgeMenuScope.Colony))
            {
                state.scope = KnowledgeMenuScope.Colony;
                state.subjectScroll = Vector2.zero;
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y + 48f, rect.width, 32f),
                state.scope == KnowledgeMenuScope.Colony ? "KnowledgeFramework_Colony".Translate() : "KnowledgeFramework_Colonists".Translate());
            Text.Font = GameFont.Small;
            if (state.scope == KnowledgeMenuScope.Colony)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(rect.x, rect.y + 88f, rect.width, 72f),
                    "KnowledgeFramework_ColonyDescription".Translate());
                GUI.color = Color.white;
                return;
            }

            Rect outer = new Rect(rect.x, rect.y + 84f, rect.width, Mathf.Max(0f, rect.height - 84f));
            Rect view = new Rect(0f, 0f, outer.width - 16f, Mathf.Max(outer.height, pawns.Count * 54f));
            Widgets.BeginScrollView(outer, ref state.pawnScroll, view);
            try
            {
                int first = Mathf.Max(0, Mathf.FloorToInt(state.pawnScroll.y / 54f) - 1);
                int last = Mathf.Min(pawns.Count, Mathf.CeilToInt((state.pawnScroll.y + outer.height) / 54f) + 1);
                for (int i = first; i < last; i++)
                {
                    Pawn pawn = pawns[i];
                    Rect row = new Rect(0f, i * 54f, view.width, 48f);
                    if (pawn == state.selectedPawn) Widgets.DrawHighlightSelected(row);
                    else Widgets.DrawHighlightIfMouseover(row);
                    Widgets.ThingIcon(new Rect(4f, row.y + 4f, 40f, 40f), pawn);
                    Widgets.Label(new Rect(52f, row.y + 4f, row.width - 58f, 24f), pawn.LabelShortCap);
                    GUI.color = Color.gray;
                    Widgets.Label(new Rect(52f, row.y + 26f, row.width - 58f, 20f),
                        (expertiseFor?.Invoke(pawn) ?? KnowledgeRank.Novice).ToString());
                    GUI.color = Color.white;
                    if (Widgets.ButtonInvisible(row))
                    {
                        state.selectedPawn = pawn;
                        state.subjectScroll = Vector2.zero;
                        state.cachedModel = null;
                    }
                }
            }
            finally { Widgets.EndScrollView(); }
            if (pawns.Count == 0) Widgets.Label(outer.ContractedBy(8f), "KnowledgeFramework_NoColonist".Translate());
        }

        private static void DrawDetail(Rect rect, KnowledgeMenuState state, KnowledgeMenuModel model, bool colony)
        {
            if (model == null)
            {
                Widgets.Label(rect, colony ? "KnowledgeFramework_NoColonyKnowledge".Translate() : "KnowledgeFramework_NoPersonalKnowledge".Translate());
                return;
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 34f), model.title ?? "KnowledgeFramework_Title".Translate());
            Text.Font = GameFont.Small;
            if (!colony)
            {
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(rect.xMax - 180f, rect.y + 4f, 180f, 28f),
                    ("KnowledgeFramework_Rank_" + model.expertiseRank).Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                DrawProgressBar(new Rect(rect.x, rect.y + 42f, rect.width, 30f), model.expertiseProgress,
                    model.expertiseRank == KnowledgeRank.Master
                        ? "KnowledgeFramework_ExpertiseMaster".Translate(model.expertiseLabel)
                        : "KnowledgeFramework_ExpertiseToward".Translate(model.expertiseLabel, ((KnowledgeRank)((int)model.expertiseRank + 1)).ToString()));
            }

            List<KnowledgeMenuSection> sections = model.sections?.Where(section => section != null).ToList()
                ?? new List<KnowledgeMenuSection>();
            if (sections.Count == 0)
            {
                Widgets.Label(new Rect(rect.x, rect.y + 90f, rect.width, 60f), "KnowledgeFramework_NoCategories".Translate());
                return;
            }
            if (state.selectedSection.NullOrEmpty() || sections.All(section => section.id != state.selectedSection))
                state.selectedSection = sections[0].id;

            float y = rect.y + (colony ? 42f : 88f);
            if (sections.Count > 1)
            {
                float tabWidth = Mathf.Min(180f, (rect.width - (sections.Count - 1) * 8f) / sections.Count);
                for (int i = 0; i < sections.Count; i++)
                {
                    KnowledgeMenuSection section = sections[i];
                    if (Widgets.ButtonText(new Rect(rect.x + i * (tabWidth + 8f), y, tabWidth, 32f), section.label,
                            active: state.selectedSection != section.id))
                    {
                        state.selectedSection = section.id;
                        state.subjectScroll = Vector2.zero;
                    }
                }
                y += 42f;
            }

            Widgets.Label(new Rect(rect.x, y + 4f, 56f, 24f), "KnowledgeFramework_Search".Translate());
            state.search = Widgets.TextField(new Rect(rect.x + 60f, y, rect.width - 96f, 30f), state.search ?? string.Empty);
            if (!state.search.NullOrEmpty() && Widgets.ButtonText(new Rect(rect.xMax - 30f, y, 30f, 30f), "X"))
                state.search = string.Empty;
            y += 40f;

            KnowledgeMenuSection selected = sections.First(section => section.id == state.selectedSection);
            string search = state.search?.Trim();
            List<KnowledgeMenuRow> rows = selected.rows.Where(row => row != null &&
                    (search.NullOrEmpty() || (row.label ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                     (row.status ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(row => row.label).ToList();
            Rect outer = new Rect(rect.x, y, rect.width, rect.yMax - y);
            Rect view = new Rect(0f, 0f, outer.width - 16f, Mathf.Max(outer.height, rows.Count * RowHeight));
            Widgets.BeginScrollView(outer, ref state.subjectScroll, view);
            try
            {
                int first = Mathf.Max(0, Mathf.FloorToInt(state.subjectScroll.y / RowHeight) - 1);
                int last = Mathf.Min(rows.Count, Mathf.CeilToInt((state.subjectScroll.y + outer.height) / RowHeight) + 1);
                for (int i = first; i < last; i++) DrawRow(new Rect(0f, i * RowHeight, view.width, 42f), rows[i]);
            }
            finally { Widgets.EndScrollView(); }
            if (rows.Count == 0)
                Widgets.Label(outer.ContractedBy(8f), search.NullOrEmpty() ? selected.emptyText : (string)"KnowledgeFramework_NoMatches".Translate());
        }

        private static void DrawRow(Rect row, KnowledgeMenuRow value)
        {
            Widgets.DrawHighlightIfMouseover(row);
            float textX = 4f;
            if (value.iconDef != null)
            {
                Widgets.ThingIcon(new Rect(4f, row.y + 5f, 32f, 32f), value.iconDef);
                textX = 46f;
            }
            Widgets.Label(new Rect(textX, row.y + 2f, row.width * 0.46f - textX, 22f), value.label ?? "KnowledgeFramework_Unknown".Translate());
            GUI.color = Color.gray;
            Widgets.Label(new Rect(textX, row.y + 22f, row.width * 0.48f - textX, 20f), value.status ?? value.rank.ToString());
            GUI.color = Color.white;
            DrawProgressBar(new Rect(row.width * 0.52f, row.y + 8f, row.width * 0.46f, 26f), value.progress,
                "KnowledgeFramework_KnowledgePercent".Translate(Mathf.Clamp01(value.progress).ToStringPercent()));
            TooltipHandler.TipRegion(row, value.tooltip ?? ((value.label ?? "KnowledgeFramework_Knowledge".Translate()) + " - " + value.rank));
            if (Widgets.ButtonInvisible(row))
            {
                if (value.actions != null && value.actions.Count > 0) Find.WindowStack.Add(new FloatMenu(value.actions));
                else value.select?.Invoke();
            }
        }

        private static List<Pawn> cachedPawns;
        private static int pawnCacheTick = -1000;

        internal static void ResetGameState()
        {
            cachedPawns = null;
            pawnCacheTick = -1000;
        }

        private static List<Pawn> PawnList()
        {
            int tick = Find.TickManager?.TicksGame ?? 0;
            if (cachedPawns != null && tick >= pawnCacheTick && tick - pawnCacheTick < 60) return cachedPawns;
            cachedPawns = Find.Maps.SelectMany(map => map.mapPawns.FreeColonists)
                .Where(pawn => pawn?.Faction?.def?.isPlayer == true && !pawn.Dead)
                .Distinct().OrderBy(pawn => pawn.LabelShort).ToList();
            pawnCacheTick = tick;
            return cachedPawns;
        }

        private static void DrawProgressBar(Rect rect, float value, string label)
        {
            Widgets.FillableBar(rect, Mathf.Clamp01(value));
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, label);
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}
