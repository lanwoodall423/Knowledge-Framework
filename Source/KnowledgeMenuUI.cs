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
    }

    public sealed class KnowledgeMenuRow
    {
        public string label;
        public ThingDef iconDef;
        public KnowledgeRank rank;
        public float progress;
        public string status;
        public string tooltip;
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
        public string expertiseLabel = "Expertise";
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
            List<Pawn> pawns = Find.Maps.SelectMany(map => map.mapPawns.FreeColonists)
                .Where(pawn => pawn?.Faction?.def?.isPlayer == true && !pawn.Dead)
                .Distinct().OrderBy(pawn => pawn.LabelShort).ToList();
            if (state.selectedPawn == null || !pawns.Contains(state.selectedPawn)) state.selectedPawn = pawns.FirstOrDefault();

            float leftWidth = Mathf.Clamp(rect.width * 0.32f, 248f, 380f);
            Rect left = new Rect(rect.x, rect.y, leftWidth, rect.height);
            Rect right = new Rect(left.xMax + Gap, rect.y, rect.width - left.width - Gap, rect.height);
            Widgets.DrawMenuSection(left);
            Widgets.DrawMenuSection(right);
            DrawNavigation(left.ContractedBy(10f), state, pawns, expertiseFor);

            bool colony = state.scope == KnowledgeMenuScope.Colony;
            KnowledgeMenuModel model = modelFor(state.selectedPawn, colony);
            DrawDetail(right.ContractedBy(14f), state, model, colony);
        }

        private static void DrawNavigation(Rect rect, KnowledgeMenuState state, List<Pawn> pawns,
            Func<Pawn, KnowledgeRank> expertiseFor)
        {
            float modeWidth = (rect.width - 8f) * 0.5f;
            if (Widgets.ButtonText(new Rect(rect.x, rect.y, modeWidth, 34f), "Colonist",
                    active: state.scope != KnowledgeMenuScope.Colonist))
            {
                state.scope = KnowledgeMenuScope.Colonist;
                state.subjectScroll = Vector2.zero;
            }
            if (Widgets.ButtonText(new Rect(rect.x + modeWidth + 8f, rect.y, modeWidth, 34f), "Colony",
                    active: state.scope != KnowledgeMenuScope.Colony))
            {
                state.scope = KnowledgeMenuScope.Colony;
                state.subjectScroll = Vector2.zero;
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y + 48f, rect.width, 32f),
                state.scope == KnowledgeMenuScope.Colony ? "Colony" : "Colonists");
            Text.Font = GameFont.Small;
            if (state.scope == KnowledgeMenuScope.Colony)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(rect.x, rect.y + 88f, rect.width, 72f),
                    "Shared subject knowledge across the colony.");
                GUI.color = Color.white;
                return;
            }

            Rect outer = new Rect(rect.x, rect.y + 84f, rect.width, rect.height - 84f);
            Rect view = new Rect(0f, 0f, outer.width - 16f, Mathf.Max(outer.height, pawns.Count * 54f));
            Widgets.BeginScrollView(outer, ref state.pawnScroll, view);
            for (int i = 0; i < pawns.Count; i++)
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
                }
            }
            Widgets.EndScrollView();
            if (pawns.Count == 0) Widgets.Label(outer.ContractedBy(8f), "No colonist is available.");
        }

        private static void DrawDetail(Rect rect, KnowledgeMenuState state, KnowledgeMenuModel model, bool colony)
        {
            if (model == null)
            {
                Widgets.Label(rect, colony ? "No colony knowledge is available." : "No colonist knowledge is available.");
                return;
            }

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 34f), model.title ?? "Knowledge & Expertise");
            Text.Font = GameFont.Small;
            if (!colony)
            {
                Text.Anchor = TextAnchor.MiddleRight;
                Widgets.Label(new Rect(rect.xMax - 180f, rect.y + 4f, 180f, 28f), model.expertiseRank.ToString());
                Text.Anchor = TextAnchor.UpperLeft;
                DrawProgressBar(new Rect(rect.x, rect.y + 42f, rect.width, 30f), model.expertiseProgress,
                    model.expertiseRank == KnowledgeRank.Master
                        ? model.expertiseLabel + " - Master"
                        : model.expertiseLabel + " toward " + (KnowledgeRank)((int)model.expertiseRank + 1));
            }

            List<KnowledgeMenuSection> sections = model.sections?.Where(section => section != null).ToList()
                ?? new List<KnowledgeMenuSection>();
            if (sections.Count == 0)
            {
                Widgets.Label(new Rect(rect.x, rect.y + 90f, rect.width, 60f), "No knowledge categories are available.");
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

            Widgets.Label(new Rect(rect.x, y + 4f, 56f, 24f), "Search");
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
            for (int i = 0; i < rows.Count; i++) DrawRow(new Rect(0f, i * RowHeight, view.width, 42f), rows[i]);
            Widgets.EndScrollView();
            if (rows.Count == 0)
                Widgets.Label(outer.ContractedBy(8f), search.NullOrEmpty() ? selected.emptyText : "No matching knowledge records.");
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
            Widgets.Label(new Rect(textX, row.y + 2f, row.width * 0.46f - textX, 22f), value.label ?? "Unknown");
            GUI.color = Color.gray;
            Widgets.Label(new Rect(textX, row.y + 22f, row.width * 0.48f - textX, 20f), value.status ?? value.rank.ToString());
            GUI.color = Color.white;
            DrawProgressBar(new Rect(row.width * 0.52f, row.y + 8f, row.width * 0.46f, 26f), value.progress,
                "Knowledge " + Mathf.Clamp01(value.progress).ToStringPercent());
            TooltipHandler.TipRegion(row, value.tooltip ?? ((value.label ?? "Knowledge") + " - " + value.rank));
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
