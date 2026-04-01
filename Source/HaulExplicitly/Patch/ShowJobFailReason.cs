using HarmonyLib;
using HaulExplicitly.Extension;
using JetBrains.Annotations;
using Verse;
using Verse.AI;

namespace HaulExplicitly.Patch;

[HarmonyPatch(typeof(HaulAIUtility), "HaulToStorageJob")]
public class ShowJobFailReason
{
    [HarmonyPrefix]
    [UsedImplicitly]
    static void ChangeJobFailReason(Thing t)
    {
        HaulAIUtility.NoEmptyPlaceLowerTrans = t.GetDontMoved() ? "HaulExplicitly.ThisItemHasBeenSetDontHaul".Translate() : "NoEmptyPlaceLower".Translate();
    }
}