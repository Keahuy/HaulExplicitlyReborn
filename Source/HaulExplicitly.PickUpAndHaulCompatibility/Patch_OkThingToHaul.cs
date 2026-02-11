using HarmonyLib;
using HaulExplicitly.Extension;
using JetBrains.Annotations;
using Verse;

namespace HaulExplicitly.PickUpAndHaulCompatibility;

[HarmonyPatch(typeof(PickUpAndHaul.WorkGiver_HaulToInventory), "OkThingToHaul")]
[UsedImplicitly]
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