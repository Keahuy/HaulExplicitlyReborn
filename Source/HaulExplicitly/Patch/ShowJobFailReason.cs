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
        if (t.GetDontMoved())
        {
            HaulAIUtility.NoEmptyPlaceLowerTrans = "HaulExplicitly.ThisItemHasBeenSetDontHaul".Translate();
        }
        else
        {
            HaulAIUtility.NoEmptyPlaceLowerTrans = "NoEmptyPlaceLower".Translate();
        }
    }
}