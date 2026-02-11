using HarmonyLib;
using HaulExplicitly.Extension;
using JetBrains.Annotations;
using Verse;
using Verse.AI;

namespace HaulExplicitly.WhileYoureUpCompatibility;

[HarmonyPatch(typeof(JobDriver_HaulToCell), "MakeNewToils")]
[UsedImplicitly]
public class WhileYoureUpCompatibilityPatch
{
    [UsedImplicitly]
    static void Prefix(JobDriver_HaulToCell __instance)
    {
        __instance.FailOn((Condition));

        bool Condition()
        {
            return __instance.ToHaul.GetDontMoved();
        }
    }
}