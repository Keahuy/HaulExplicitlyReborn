using System.Reflection.Emit;
using HarmonyLib;
using HaulExplicitly.Gizmo;
using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace HaulExplicitly.Patch;

//  Show Gizmo
// 我找了一会，发现了这种方法，也许还有更简单更直接的方法？我懒得找了，反正这样也能行
/// <summary>
/// 补丁：向原版的反向设计器列表（<c>ReverseDesignatorDatabase.desList</c>）中插入本模组的
/// 「恢复可搬运」与「禁止搬运」设计器，使它们出现在游戏界面的设计器栏里。
/// </summary>
[HarmonyPatch(typeof(ReverseDesignatorDatabase), "InitDesignators")]
public class ReverseDesignatorDatabase_InitDesignators_Patch
{
    /// <summary>
    /// 转译器（Transpiler）：在原版 <c>InitDesignators</c> 注册完 <c>Designator_ExtractSkull</c> 之后，
    /// 追加对本模组两个设计器的注册。
    /// </summary>
    /// <param name="instructions">原版方法的 IL 指令序列。</param>
    /// <returns>插入新指令后的指令序列。</returns>
    [HarmonyTranspiler]
    [UsedImplicitly]
    static IEnumerable<CodeInstruction> AddDesignator(IEnumerable<CodeInstruction> instructions)
    {
        var desListField = AccessTools.Field(typeof(ReverseDesignatorDatabase), "desList");
        var targetCtor = AccessTools.Constructor(typeof(Designator_ExtractSkull), Type.EmptyTypes);
        var listAddMethod = AccessTools.Method(typeof(List<Designator>), nameof(List<Designator>.Add), [typeof(Designator)]);
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
        }
    }
}

/// <summary>
/// 补丁：为物品追加本模组的 Gizmo——显式搬运、取消显式搬运、全选同批物品、搬运后自动禁用。
/// </summary>
[HarmonyPatch(typeof(Thing), "GetGizmos")]
class Thing_GetGizmos_Patch
{
    /// <summary>
    /// 先原样输出原版返回的全部 Gizmo，再依据物品的状态追加本模组的按钮。
    /// </summary>
    /// <param name="gizmos">原版方法返回的 Gizmo 序列。</param>
    /// <param name="__instance">正在获取 Gizmo 的物品。</param>
    /// <returns>合并后的 Gizmo 序列。</returns>
    [HarmonyPostfix]
    [UsedImplicitly]
    static IEnumerable<Verse.Gizmo> AddHaulExplicitlyGizmos(IEnumerable<Verse.Gizmo> gizmos, Thing __instance)
    {
        // 返回原本应该返回的内容
        foreach (Verse.Gizmo gizmo in gizmos)
        {
            yield return gizmo;
        }
        
        // 添加本mod的gizmos
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
        if (Command_AutoForbiddenAfterHaulExplicitly.RelevantToThing(__instance))
        {
            Command_AutoForbiddenAfterHaulExplicitly result = new Command_AutoForbiddenAfterHaulExplicitly(__instance);
            result.UpdateLabelAndIcon(__instance);
            yield return result;
        }
    }
}