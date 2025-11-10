using HarmonyLib;
using JetBrains.Annotations;
using Verse;

namespace HaulExplicitly.Patch;

[HarmonyPatch(typeof(CompressibilityDecider), "DetermineReferences")]
public class PleaseDontCompressMyChunk
{
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
            ((HashSet<Thing>)__instance.GetType().GetField("referencedThings", AccessTools.all).GetValue(__instance)).Add(item);
        }
    }
}