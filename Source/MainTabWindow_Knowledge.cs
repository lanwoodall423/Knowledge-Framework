using RimWorld;
using UnityEngine;
using Verse;

namespace KnowledgeFramework
{
    public sealed class MainTabWindow_Knowledge : MainTabWindow
    {
        public override Vector2 InitialSize => new Vector2(Mathf.Min(700f, UI.screenWidth * 0.7f), 220f);

        public override void DoWindowContents(Rect inRect)
        {
            if (!KnowledgeV3Ui.HasDomains)
            {
                Widgets.Label(inRect, "KnowledgeFramework_NoDomains".Translate());
                return;
            }

            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 34f), "KnowledgeFramework_Title".Translate());
            Widgets.Label(new Rect(inRect.x, inRect.y + 42f, inRect.width, 48f),
                "KnowledgeFramework_ColonyBrowserDescription".Translate());
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.y + 106f, 260f, 36f), "KnowledgeFramework_OpenColonyBrowser".Translate()))
            {
                Close(false);
                KnowledgeV3Ui.OpenColony();
            }
        }
    }
}
