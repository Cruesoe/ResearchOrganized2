using ResearchOrganized2;
using Verse;

namespace ResearchOrganized2UIRethemed
{
    [StaticConstructorOnStartup]
    public static class UIRethemedSkinStartup
    {
        static UIRethemedSkinStartup()
        {
            ResearchSkin.Register(new UIRethemedSkin());
        }
    }
}
