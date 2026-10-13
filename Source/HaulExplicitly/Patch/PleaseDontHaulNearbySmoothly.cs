using System.Reflection;
using HarmonyLib;
using HaulExplicitly.Extension;
using JetBrains.Annotations;
using Verse;
using Verse.AI;

namespace HaulExplicitly.Patch;

/// <summary>
/// 补丁：原版「顺手搬运附近同类物品」（<c>Toils_Haul.CheckForGetOpportunityDuplicate</c>）的校验逻辑，
/// 额外排除被标记为「不要自动搬走」的物品。
/// </summary>
[HarmonyPatch(typeof(Toils_Haul))]
public class PleaseDontHaulNearbySmoothly
{
    /// <summary>
    /// 动态指定补丁目标：原版把这段校验逻辑放在编译器生成的闭包类型里，
    /// 因此这里通过反射定位其中的 <c>DupeValidator</c> 方法。
    /// </summary>
    /// <returns>找到的目标方法；找不到时记录错误并返回 <c>null</c>（该补丁将不会被应用）。</returns>
    [UsedImplicitly]
    static MethodBase? TargetMethod()
    {
        // Verse.AI.Toils_Haul.CheckForGetOpportunityDuplicate
        var displayClass = typeof(Toils_Haul).GetNestedType("<>c__DisplayClass5_1", BindingFlags.NonPublic);
        if (displayClass == null)
        {
            Log.Error("[HaulExplicitly] TargetMethod failed: Could not find display class.");
            return null;
        }

        var method = displayClass.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).FirstOrDefault(m => m.Name.Contains("DupeValidator"));
        if (method == null)
        {
            Log.Error("[HaulExplicitly] TargetMethod failed: Could not find DupeValidator method.");
        }

        return method;
    }

    /// <summary>
    /// 后缀补丁：原判定为可顺手搬运时，若该物品已被标记为「不要自动搬走」，则改为不可顺手搬运。
    /// </summary>
    /// <param name="__instance">原版的判定结果。</param>
    /// <param name="t">被检查的物品。</param>
    /// <returns>修正后的判定结果。</returns>
    [HarmonyPostfix]
    static bool AddCheckWithDontMove(bool __instance, Thing t)
    {
        if (__instance)
        {
            if (t.GetDontMoved())
            {
                return false;
            }
        }

        return __instance;
    }
}