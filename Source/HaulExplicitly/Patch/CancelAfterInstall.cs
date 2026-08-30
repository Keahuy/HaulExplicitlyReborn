using HarmonyLib;
using HaulExplicitly.Extension;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulExplicitly.Patch;

[HarmonyPatch(typeof(JobDriver_HaulToContainer), "ModifyPrepareToil")]
public class CancelAfterInstall
{
    
    [HarmonyPostfix]
    static void AddStep (JobDriver_HaulToContainer __instance, Toil toil)
    {
        toil.AddFinishAction(delegate
        {
            __instance.ThingToCarry.SetDontMoved(false);
        });
    }
}