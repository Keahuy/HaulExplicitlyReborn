using HarmonyLib;
using HaulExplicitly.Extension;
using JetBrains.Annotations;
using Verse;

namespace HaulExplicitlyCompatibilityWithPickUpAndHaul;

[StaticConstructorOnStartup]
public class PatchMain
{
    static PatchMain()
    {
        var harmony = new Harmony("likeafox.rimworld.haulexplicitly");
        harmony.PatchAll();
    }
}

[HarmonyPatch(typeof(PickUpAndHaul.WorkGiver_HaulToInventory), "OkThingToHaul")]
public class Patch_ThingWithMarkerIsNotOK
{
    [HarmonyPostfix]
    [UsedImplicitly]
    static bool AddCondition(bool __result, Thing t)
    {
        if (t.GetDontMoved())
        {
            __result = false;
        }

        return __result;
    }
}