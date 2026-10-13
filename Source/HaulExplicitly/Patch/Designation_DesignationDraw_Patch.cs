using HarmonyLib;
using HaulExplicitly.Extension;
using JetBrains.Annotations;
using UnityEngine;
using Verse;

namespace HaulExplicitly.Patch;

/// <summary>
/// 补丁：调整「禁止搬运」标记的绘制方式——不再绘制原版标记图标，
/// 而是在被标记的物品旁绘制一个锚点图标。
/// </summary>
[HarmonyPatch(typeof(Designation), "DesignationDraw")]
class Designation_DesignationDraw_Patch
{
    /// <summary>
    /// 前缀补丁：若标记不是 Unhaul 类型，或物品的标记已被取消，则交给原版绘制；
    /// 若物品已被标记为「不要自动搬走」，则在物品旁绘制锚点图标并跳过原版绘制。
    /// </summary>
    /// <param name="__instance">正在绘制的标记。</param>
    /// <returns>返回 <c>false</c> 表示跳过原版方法，返回 <c>true</c> 表示继续执行原版绘制。</returns>
    [HarmonyPrefix]
    [UsedImplicitly]
    static bool AdjustDesignationSizeAndCoordinates(Designation __instance)
    {
        if (__instance.def != HaulExplicitlyDefOf.HaulExplicitly_Unhaul) return true;
        Thing t = __instance.target.Thing;
        if (t == null || !t.Spawned) return false;
        Vector3 pos = t.DrawPos;
        pos.x += t.RotatedSize.x * 0.3f;
        pos.y = AltitudeLayer.MetaOverlays.AltitudeFor() + 0.155f;
        string? matpath;
        Mesh mesh;
        
        if (t.GetDontMoved())
        {
            matpath = "Overlay/Anchor";
            pos.z -= t.RotatedSize.z * 0.3f;
            mesh = MeshPool.plane03;
            Graphics.DrawMesh(mesh, pos, Quaternion.identity, MaterialPool.MatFrom(matpath, ShaderDatabase.MetaOverlay), 0);
            return false;
        }

        return true;
    }
}