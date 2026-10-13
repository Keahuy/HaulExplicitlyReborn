using HarmonyLib;
using HaulExplicitly.Extension;
using JetBrains.Annotations;
using Verse;

namespace HaulExplicitly.PickUpAndHaulCompatibility;

/// <summary>
/// 与 PickUpAndHaul 的兼容补丁：让「拾取并搬运」模组不再把被标记为「不要自动搬走」的物品
/// 视为可搬运目标。
/// </summary>
[HarmonyPatch(typeof(PickUpAndHaul.WorkGiver_HaulToInventory), "OkThingToHaul")]
[UsedImplicitly]
public class Patch_ThingWithMarkerIsNotOK
{
    /// <summary>
    /// 后缀补丁：物品被标记为「不要自动搬走」时把判定结果改为 <c>false</c>。
    /// </summary>
    /// <param name="__result">原方法的返回值。</param>
    /// <param name="t">被检查的物品。</param>
    /// <returns>修正后的判定结果。</returns>
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