using HarmonyLib;
using HaulExplicitly.Extension;
using JetBrains.Annotations;
using Verse;
using Verse.AI;

namespace HaulExplicitly.Patch;

/// <summary>
/// 补丁：替换自动搬运寻找储存位置时的失败提示：对于被标记为「不要自动搬走」的物品，
/// 显示专门的说明文字，而不是原版的「找不到空位」。
/// </summary>
[HarmonyPatch(typeof(HaulAIUtility), "HaulToStorageJob")]
public class ShowJobFailReason
{
    /// <summary>
    /// 前缀补丁：依据物品是否被标记为「不要自动搬走」，设置原版使用的失败原因文本。
    /// </summary>
    /// <param name="t">正在尝试入库的物品。</param>
    [HarmonyPrefix]
    [UsedImplicitly]
    static void ChangeJobFailReason(Thing t)
    {
        HaulAIUtility.NoEmptyPlaceLowerTrans = t.GetDontMoved() ? "HaulExplicitly.ThisItemHasBeenSetDontHaul".Translate() : "NoEmptyPlaceLower".Translate();
    }
}