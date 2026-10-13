using HarmonyLib;
using HaulExplicitly.Extension;
using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace HaulExplicitly.Patch;

/// <summary>
/// 补丁：让被标记为「不要自动搬走」的物品不再参与原版的「寻找更优储存区」流程，
/// 从而不会被自动搬进优先级更高的储存区。
/// </summary>
[HarmonyPatch(typeof(StoreUtility),"TryFindBestBetterStorageFor")]
public class StoreUtility_TryFindBestBetterStorageFor_Patch
{
    // 通过使物品 TryFindBestBetterStorageFor return false 使其不被搬运至优先级更高的存储区
    // 使用 HarmonyPostfix将HaulExplicitly_dontMoved 作为判断 TryFindBestBetterStorageFor 的附加条件
    /// <summary>
    /// 后缀补丁：物品被标记为「不要自动搬走」时把结果改为 <c>false</c>，否则保留原结果。
    /// </summary>
    /// <param name="value">原方法的返回值。</param>
    /// <param name="t">目标物品。</param>
    /// <returns>修正后的返回值。</returns>
    [HarmonyPostfix]
    [UsedImplicitly]
    static bool AddCondition(bool value, Thing t)
    {
        if (t.GetDontMoved())
        {
            return false;
        }
        return value;
    }
}