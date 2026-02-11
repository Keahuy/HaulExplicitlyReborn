using HarmonyLib;
using HaulExplicitly.Extension;
using JetBrains.Annotations;
using Verse.AI;

namespace HaulExplicitly.WhileYoureUpCompatibility;

[HarmonyPatch(typeof(JobDriver_HaulToCell), "TryMakePreToilReservations")]
[UsedImplicitly]
public class WhileYoureUpCompatibilityPatch
{
    /// <summary>
    /// Compatibility for: While You're Up (1.6 patch)
    /// </summary>
    /// <param name="__instance"></param>
    /// <param name="__result"></param>
    [UsedImplicitly]
    static void Postfix(JobDriver_HaulToCell __instance, ref bool __result)
    {
        if (__instance.ToHaul.GetDontMoved())
        {
            __result = false;
        }
    }
}