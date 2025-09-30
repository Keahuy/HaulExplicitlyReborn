using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using HaulExplicitly.Extension;
using JetBrains.Annotations;
using Verse;
using Verse.AI;

namespace HaulExplicitly.Patch;

[HarmonyPatch(typeof(Toils_Haul))]
public class PleaseDontHaulNearbySmoothly
{
    [UsedImplicitly]
    static MethodBase? TargetMethod()
    {
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