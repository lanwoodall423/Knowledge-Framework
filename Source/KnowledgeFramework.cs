using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace KnowledgeFramework
{
    public enum KnowledgeRank
    {
        Novice,
        Adept,
        Expert,
        Master
    }

    public static class KnowledgeRanks
    {
        public static KnowledgeRank ForExperience(float experience, float adept, float expert, float master)
        {
            if (experience >= master) return KnowledgeRank.Master;
            if (experience >= expert) return KnowledgeRank.Expert;
            if (experience >= adept) return KnowledgeRank.Adept;
            return KnowledgeRank.Novice;
        }

        public static float Progress(float experience, float adept, float expert, float master)
        {
            KnowledgeRank rank = ForExperience(experience, adept, expert, master);
            if (rank == KnowledgeRank.Master) return 1f;
            float lower = rank == KnowledgeRank.Novice ? 0f : rank == KnowledgeRank.Adept ? adept : expert;
            float upper = rank == KnowledgeRank.Novice ? adept : rank == KnowledgeRank.Adept ? expert : master;
            return Mathf.InverseLerp(lower, upper, experience);
        }

        public static float Bonus(KnowledgeRank rank, float perTier) => (int)rank * perTier;
    }

    public class KnowledgeRecord : IExposable
    {
        public Pawn pawn;
        public string subjectDefName;
        public float experience;

        public KnowledgeRecord() { }

        public KnowledgeRecord(Pawn pawn, string subjectDefName)
        {
            this.pawn = pawn;
            this.subjectDefName = subjectDefName;
        }

        public virtual void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref subjectDefName, "subjectDefName");
            Scribe_Values.Look(ref experience, "experience");
            experience = KnowledgeMath.NonNegativeFiniteOr(experience, 0f);
        }
    }

    public sealed class KnowledgeEntry
    {
        public string providerId;
        public string label;
        public KnowledgeRank rank;
        public float progress;
        public string summary;
        public string tooltip;
        public Action openDetails;
    }

    public static class KnowledgeProviderRegistry
    {
        private sealed class Provider
        {
            public string id;
            public int order;
            public Func<Pawn, KnowledgeEntry> entry;
        }

        private sealed class PawnCache
        {
            public int cacheTick;
            public int revision;
            public IReadOnlyList<KnowledgeEntry> entries;
        }

        private static readonly Dictionary<string, Provider> Providers = new Dictionary<string, Provider>();
        private static readonly Dictionary<int, PawnCache> Cache = new Dictionary<int, PawnCache>();
        private static int revision;

        public static void Register(string id, int order, Func<Pawn, KnowledgeEntry> entry)
        {
            if (id.NullOrEmpty() || entry == null) return;
            Providers[id] = new Provider { id = id, order = order, entry = entry };
            revision++;
            Cache.Clear();
        }

        public static bool Unregister(string id)
        {
            if (id.NullOrEmpty() || !Providers.Remove(id)) return false;
            revision++;
            Cache.Clear();
            return true;
        }

        public static IReadOnlyList<KnowledgeEntry> EntriesFor(Pawn pawn)
        {
            if (pawn == null) return Array.Empty<KnowledgeEntry>();
            int now = Find.TickManager?.TicksGame ?? 0;
            int key = pawn.thingIDNumber;
            if (Cache.TryGetValue(key, out PawnCache cached) && cached.revision == revision && now >= cached.cacheTick && now - cached.cacheTick < 60)
            {
                KnowledgeDiagnostics.CacheHit();
                return cached.entries;
            }
            KnowledgeDiagnostics.CacheMiss();
            List<KnowledgeEntry> entries = Providers.Values.OrderBy(provider => provider.order).ThenBy(provider => provider.id)
                .Select(provider => SafeEntry(provider, pawn)).Where(entry => entry != null).ToList();
            Cache[key] = new PawnCache { cacheTick = now, revision = revision,
                entries = new System.Collections.ObjectModel.ReadOnlyCollection<KnowledgeEntry>(entries) };
            return Cache[key].entries;
        }

        public static void Invalidate(Pawn pawn)
        {
            if (pawn != null) Cache.Remove(pawn.thingIDNumber);
        }

        public static void InvalidateAll()
        {
            Cache.Clear();
        }

        private static KnowledgeEntry SafeEntry(Provider provider, Pawn pawn)
        {
            try
            {
                KnowledgeEntry entry = provider.entry(pawn);
                if (entry != null) entry.providerId = provider.id;
                return entry;
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[Knowledge Framework] Provider '" + provider.id + "' failed: " + exception.Message,
                    GenText.StableStringHash(provider.id));
                return null;
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class KnowledgeFrameworkStartup
    {
        static KnowledgeFrameworkStartup()
        {
            try { new Harmony("lan.knowledgeframework").PatchAll(typeof(KnowledgeFrameworkStartup).Assembly); }
            catch (Exception exception) { KnowledgeLog.ErrorOnce("startup:harmony", "Knowledge Framework Harmony patching failed.", exception); }
            LongEventHandler.ExecuteWhenFinished(KnowledgeRegistry.BuildDefSchemas);
        }
    }

    public static class KnowledgeBioPanel
    {
        private const float HeaderHeight = 30f;
        private const float RowHeight = 28f;
        private static readonly HashSet<int> expandedPawns = new HashSet<int>();
        private static readonly Color[] RankColors =
        {
            new Color(0.52f, 0.56f, 0.58f),
            new Color(0.48f, 0.68f, 0.50f),
            new Color(0.78f, 0.64f, 0.34f),
            new Color(0.86f, 0.76f, 0.44f)
        };

        public static bool VisibleFor(Pawn pawn) => (KnowledgeFrameworkMod.Settings?.BioPanelEnabled ?? true) &&
            pawn?.Faction?.def?.isPlayer == true && pawn.RaceProps?.Humanlike == true &&
            KnowledgeProviderRegistry.EntriesFor(pawn).Count > 0;

        internal static void ResetGameState() => expandedPawns.Clear();

        public static float HeightFor(Pawn pawn)
        {
            if (!VisibleFor(pawn)) return 0f;
            return HeaderHeight + (expandedPawns.Contains(pawn.thingIDNumber)
                ? KnowledgeProviderRegistry.EntriesFor(pawn).Count * RowHeight + 4f : 0f);
        }

        public static void Draw(Rect rect, Pawn pawn)
        {
            if (!VisibleFor(pawn)) return;
            IReadOnlyList<KnowledgeEntry> entries = KnowledgeProviderRegistry.EntriesFor(pawn);
            float height = HeightFor(pawn);
            Rect panel = new Rect(rect.x, rect.yMax - height, rect.width, height);
            Widgets.DrawMenuSection(panel);
            Rect header = new Rect(panel.x + 6f, panel.y + 2f, panel.width - 12f, HeaderHeight - 4f);
            bool expanded = expandedPawns.Contains(pawn.thingIDNumber);
            Widgets.DrawHighlightIfMouseover(header);
            Widgets.Label(new Rect(header.x + 6f, header.y + 2f, header.width - 44f, 24f), "KnowledgeFramework_Title".Translate());
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(header.xMax - 42f, header.y, 36f, 24f), expanded ? "-" : "+");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            TooltipHandler.TipRegion(header, expanded ? "KnowledgeFramework_Collapse".Translate() : "KnowledgeFramework_Expand".Translate());
            if (Widgets.ButtonInvisible(header))
            {
                if (expanded) expandedPawns.Remove(pawn.thingIDNumber);
                else expandedPawns.Add(pawn.thingIDNumber);
            }
            if (!expanded) return;
            for (int i = 0; i < entries.Count; i++) DrawRow(new Rect(panel.x + 8f, panel.y + HeaderHeight + i * RowHeight,
                panel.width - 16f, RowHeight), entries[i]);
        }

        private static void DrawRow(Rect rect, KnowledgeEntry entry)
        {
            Widgets.DrawHighlightIfMouseover(rect);
            Color rankColor = RankColors[Mathf.Clamp((int)entry.rank, 0, RankColors.Length - 1)];
            for (int i = 0; i < 4; i++)
                Widgets.DrawBoxSolid(new Rect(rect.x + 4f + i * 7f, rect.y + 9f, 5f, 10f), i <= (int)entry.rank ? rankColor : new Color(0.24f, 0.25f, 0.26f));
            Widgets.Label(new Rect(rect.x + 38f, rect.y + 3f, 138f, 23f), entry.label);
            Text.Anchor = TextAnchor.MiddleRight;
            GUI.color = rankColor;
            Widgets.Label(new Rect(rect.x + 178f, rect.y + 2f, 76f, 23f), KnowledgeBrowserLabels.Rank(entry.rank));
            GUI.color = Color.gray;
            Widgets.Label(new Rect(rect.x + 258f, rect.y + 2f, rect.width - 264f, 23f), entry.summary ?? string.Empty);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            TooltipHandler.TipRegion(rect, entry.tooltip ?? (entry.label + " - " + KnowledgeBrowserLabels.Rank(entry.rank)));
            if (entry.openDetails != null && Widgets.ButtonInvisible(rect)) entry.openDetails();
        }
    }

    [HarmonyPatch(typeof(CharacterCardUtility), nameof(CharacterCardUtility.PawnCardSize))]
    public static class KnowledgePawnCardSizePatch
    {
        public static void Postfix(Pawn pawn, ref Vector2 __result) => __result.y += KnowledgeBioPanel.HeightFor(pawn);
    }

    [HarmonyPatch(typeof(CharacterCardUtility), nameof(CharacterCardUtility.DrawCharacterCard))]
    public static class KnowledgeCharacterCardPatch
    {
        public static void Postfix(Rect rect, Pawn pawn) => KnowledgeBioPanel.Draw(rect, pawn);
    }
}
