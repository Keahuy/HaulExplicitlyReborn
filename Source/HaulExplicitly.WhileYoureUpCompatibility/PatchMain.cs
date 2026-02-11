using HarmonyLib;
using Verse;

namespace HaulExplicitly.WhileYoureUpCompatibility;

[StaticConstructorOnStartup]
public class PatchMain
{
    static PatchMain()
    {
        var harmony = new Harmony("likeafox.rimworld.haulexplicitly");
        harmony.PatchAll();
    }
}