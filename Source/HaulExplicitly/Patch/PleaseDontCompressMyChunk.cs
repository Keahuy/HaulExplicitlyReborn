using HarmonyLib;
using JetBrains.Annotations;
using Verse;

namespace HaulExplicitly.Patch;

/// <summary>
/// 补丁：让「可压缩性判定」（<c>CompressibilityDecider</c>）把本模组正在显式搬运的物品视为被引用，
/// 避免它们在地图被压缩保存时被回收。
/// </summary>
[HarmonyPatch(typeof(CompressibilityDecider), "DetermineReferences")]
public class PleaseDontCompressMyChunk
{
    /// <summary>
    /// 后缀补丁：把所有搬运记录中的物品加入判定器的 <c>referencedThings</c> 集合。
    /// </summary>
    /// <param name="__instance">正在执行判定的压缩判定器。</param>
    [HarmonyPostfix]
    [UsedImplicitly]
    static void AddHaulExplicitlyTarget(CompressibilityDecider __instance)
    {
        var managers = GameComponent_HaulExplicitly.GetManagers();
        var datas = managers.SelectMany(mgr => mgr.datas.Values);
        var records = datas.SelectMany(d => d.inventory);
        var items = records.SelectMany(r => r.items);
        foreach (var item in items)
        {
            ((HashSet<Thing>?)__instance.GetType().GetField("referencedThings", AccessTools.all)?.GetValue(__instance))?.Add(item);
        }
    }
}