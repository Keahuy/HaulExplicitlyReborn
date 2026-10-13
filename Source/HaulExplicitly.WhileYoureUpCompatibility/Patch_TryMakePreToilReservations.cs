using HarmonyLib;
using HaulExplicitly.Extension;
using JetBrains.Annotations;
using Verse.AI;

namespace HaulExplicitly.WhileYoureUpCompatibility;

/// <summary>
/// 与 While You're Up 的兼容补丁：对方模组会自行搬运附近的物品，
/// 这里让其在搬运目标被标记为「不要自动搬走」时中止工作。
/// </summary>
[HarmonyPatch(typeof(JobDriver_HaulToCell), "MakeNewToils")]
[UsedImplicitly]
public class WhileYoureUpCompatibilityPatch
{
    /// <summary>
    /// 前缀补丁：向原版工作追加一个失败条件——被搬运的物品一旦被标记为「不要自动搬走」，工作立即结束。
    /// </summary>
    /// <param name="__instance">正在构建步骤的原版工作驱动器。</param>
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