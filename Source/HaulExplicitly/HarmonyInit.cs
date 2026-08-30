using HarmonyLib;
using Verse;

namespace HaulExplicitly.Patch;

[StaticConstructorOnStartup]
public class HarmonyInit
{
    static HarmonyInit()
    {
        var harmony = new Harmony("likeafox.rimworld.haulexplicitly");
        harmony.PatchAll();
    }
}