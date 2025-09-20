using System.Reflection.Emit;
using HarmonyLib;
using HaulExplicitly.Gizmo;
using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace HaulExplicitly.Patch;

//  Show Gizmo
// 我找了一会，发现了这种方法，也许还有更简单更直接的方法？我懒得找了，反正这样也能行
[HarmonyPatch(typeof(ReverseDesignatorDatabase), "InitDesignators")]
public class ReverseDesignatorDatabase_InitDesignators_Patch
{
    [HarmonyTranspiler]
    [UsedImplicitly]
    static IEnumerable<CodeInstruction> AddDesignator(IEnumerable<CodeInstruction> instructions)
    {
        var desListField = AccessTools.Field(typeof(ReverseDesignatorDatabase), "desList");
        var targetCtor = AccessTools.Constructor(typeof(Designator_ExtractSkull), Type.EmptyTypes);
        var listAddMethod = AccessTools.Method(typeof(List<Designator>), nameof(List<Designator>.Add), new[] { typeof(Designator) });
        var RehaulCtor = AccessTools.Constructor(typeof(Designator_Rehaul), Type.EmptyTypes);
        var UnhaulCtor = AccessTools.Constructor(typeof(Designator_Unhaul), Type.EmptyTypes);

        bool found1 = false;
        List<CodeInstruction> codes = new List<CodeInstruction>(instructions);
        for (int i = 0; i < codes.Count; i++)
        {
            yield return codes[i];

            if (!found1 && codes[i].opcode == OpCodes.Callvirt && codes[i].OperandIs(listAddMethod))
            {
                if (codes[i - 1].opcode == OpCodes.Newobj && codes[i - 1].OperandIs(targetCtor))
                {
                    // desList.Add(new Designator_Rehaul());
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, desListField);
                    yield return new CodeInstruction(OpCodes.Newobj, RehaulCtor);
                    yield return new CodeInstruction(OpCodes.Callvirt, listAddMethod);

                    // desList.Add(new Designator_Unhaul());
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, desListField);
                    yield return new CodeInstruction(OpCodes.Newobj, UnhaulCtor);
                    yield return new CodeInstruction(OpCodes.Callvirt, listAddMethod);
                    
                    found1 = true;
                }
            }

            if (found1)
            {
                /*Log.Error("HaulExplicitly: Gizmo Patch Succeed!");*/ // for testing
                break;
            }
        }
    }
}

[HarmonyPatch(typeof(Thing), "GetGizmos")]
class Thing_GetGizmos_Patch
{
    [HarmonyPostfix]
    [UsedImplicitly]
    static IEnumerable<Verse.Gizmo> AddHaulExplicitlyGizmos(IEnumerable<Verse.Gizmo> gizmos, Thing __instance)
    {
        foreach (Verse.Gizmo gizmo in gizmos)
        {
            yield return gizmo;
        }
        if (__instance.def.EverHaulable)
        {
            yield return new Designator_HaulExplicitly();
        }
        if (Command_Cancel_HaulExplicitly.RelevantToThing(__instance))
        {
            yield return new Command_Cancel_HaulExplicitly(__instance);
        }
        if (Command_SelectAllForHaulExplicitly.RelevantToThing(__instance))
        {
            yield return new Command_SelectAllForHaulExplicitly();
        }
    }
}