using HarmonyLib;
using HaulExplicitly.Extension;
using JetBrains.Annotations;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulExplicitly.Patch;

[HarmonyPatch(typeof(StoreUtility),"TryFindBestBetterStorageFor")]
public class StoreUtility_TryFindBestBetterStorageFor_Patch
{
    // 通过使物品 TryFindBestBetterStorageFor return false 使其不被随意搬运
    // 使用 HarmonyPostfix将HaulExplicitly_dontMoved 作为判断 TryFindBestBetterStorageFor 的附加条件
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