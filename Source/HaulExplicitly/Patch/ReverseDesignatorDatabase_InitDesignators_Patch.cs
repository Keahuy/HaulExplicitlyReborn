using System.Reflection.Emit;
using HarmonyLib;
using JetBrains.Annotations;
using Verse;

namespace HaulExplicitly.Patch;

// 注册 Designator_Rehaul 与 Designator_Unhaul 的 Gizmo
[HarmonyPatch(typeof(ReverseDesignatorDatabase), "InitDesignators")]
public class ReverseDesignatorDatabase_InitDesignators_Patch
{
    [HarmonyTranspiler]
    [UsedImplicitly]
    static IEnumerable<CodeInstruction> AddDesignator(IEnumerable<CodeInstruction> instructions)
    {
        var desListField = AccessTools.Field(typeof(Verse.ReverseDesignatorDatabase), "desList");
        var targetCtor = AccessTools.Constructor(typeof(RimWorld.Designator_ExtractSkull), Type.EmptyTypes);
        var RehaulCtor = AccessTools.Constructor(typeof(HaulExplicitly.Gizmo.Designator_Rehaul), Type.EmptyTypes);
        var UnhaulCtor = AccessTools.Constructor(typeof(HaulExplicitly.Gizmo.Designator_Unhaul), Type.EmptyTypes);
        var listAddMethod = AccessTools.Method(typeof(List<Designator>), nameof(List<Designator>.Add), new[] { typeof(Designator) });

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
                Log.Message("HaulExplicitly: Gizmo Patch Succeed!");
                break;
            }
        }
    }
}