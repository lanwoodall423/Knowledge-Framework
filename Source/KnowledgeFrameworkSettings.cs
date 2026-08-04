using UnityEngine;
using Verse;

namespace KnowledgeFramework
{
    public sealed class KnowledgeFrameworkSettings : ModSettings
    {
        public bool BioPanelEnabled = true;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref BioPanelEnabled, "bioPanelEnabled", true);
        }
    }

    public sealed class KnowledgeFrameworkMod : Mod
    {
        public static KnowledgeFrameworkSettings Settings { get; private set; }

        public KnowledgeFrameworkMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<KnowledgeFrameworkSettings>();
        }

        public override string SettingsCategory() => "KnowledgeFramework_Title".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("KnowledgeFramework_EnableBioPanel".Translate(), ref Settings.BioPanelEnabled,
                "KnowledgeFramework_EnableBioPanelTooltip".Translate());
            listing.End();
        }
    }
}
