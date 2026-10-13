using HarmonyLib;
using HaulExplicitly.Extension;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulExplicitly.Patch;

/// <summary>
/// 补丁：安装（HaulToContainer 相关）工作结束时，清除被搬运物品的「不要自动搬走」标记——
/// 物品已被安装成建筑的一部分，不应再保留该标记。
/// </summary>
[HarmonyPatch(typeof(JobDriver_HaulToContainer), "ModifyPrepareToil")]
public class CancelAfterInstall
{
    
    /// <summary>
    /// 后缀补丁：给原版步骤追加结束动作，在工作结束时清除被搬运物品的标记。
    /// </summary>
    /// <param name="__instance">正在准备步骤的原版工作驱动器。</param>
    /// <param name="toil">原版方法正在准备的步骤。</param>
    [HarmonyPostfix]
    static void AddStep (JobDriver_HaulToContainer __instance, Toil toil)
    {
        toil.AddFinishAction(delegate
        {
            __instance.ThingToCarry.SetDontMoved(false);
        });
    }
}