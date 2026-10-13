using HarmonyLib;
using JetBrains.Annotations;
using UnityEngine;
using Verse;

namespace HaulExplicitly;

/// <summary>
/// 显式搬运的可视化绘制：画出物品到目的地圆圈中心的连线，以及代表目的地范围的圆圈轮廓。
/// </summary>
public class HaulExplicitlyPostingVisualizationDrawer
{
    /// <summary>
    /// 本帧已经绘制过圆圈的数据 ID，用于避免同一条数据重复绘制。
    /// </summary>
    private static List<int> postingsDrawnThisFrame = new();

    /// <summary>
    /// 可视化元素使用的绘制高度（元图层）。
    /// </summary>
    private static float alt => AltitudeLayer.MetaOverlays.AltitudeFor();

    /// <summary>
    /// 为指定物品绘制它到目的地圆圈的连线；若本帧还没有为它所属的数据画过圆圈的轮廓，则补画一次。
    /// </summary>
    /// <param name="item">要绘制的物品，必须已经生成在地图上。</param>
    public static void DrawForItem(Thing item)
    {
        // for now, I don't want to draw lines for selected carried items. May change in the future.
        // Also note that this makes the later .PositionHeld/.Position distinction irrelevant
        if (!item.Spawned) return;
        var mgr = GameComponent_HaulExplicitly.GetManager(item);
        Data_DesignatorHaulExplicitly? data = mgr.DataWithItem(item);
        if (data == null) return;
        //draw line
        Vector3 start = item
            .PositionHeld
            .ToVector3ShiftedWithAltitude(alt);
        Vector3 circle_center = data.center;
        circle_center.y = alt;
        Vector3 line_vector = circle_center - start;
        if (line_vector.magnitude > data.visualizationRadius)
        {
            line_vector = line_vector.normalized * (line_vector.magnitude - data.visualizationRadius);
            GenDraw.DrawLineBetween(start, start + line_vector);
        }

        if (postingsDrawnThisFrame.Contains(data.ID))
            return;
        postingsDrawnThisFrame.Add(data.ID);
        //draw circle
        GenDraw.DrawCircleOutline(circle_center, data.visualizationRadius);
    }

    /// <summary>
    /// 清空本帧的绘制记录，使下一帧可以重新绘制圆圈。
    /// </summary>
    public static void Clear()
    {
        postingsDrawnThisFrame.Clear();
    }
}

/// <summary>
/// 补丁：在物品绘制自身的额外选择覆盖层时，为可搬运的物品追加显式搬运的可视化提示。
/// </summary>
[HarmonyPatch(typeof(Thing), "DrawExtraSelectionOverlays")]
class Thing_DrawExtraSelectionOverlays_Patch
{
    /// <summary>
    /// 原版方法执行之后，为可搬运的物品绘制显式搬运提示。
    /// </summary>
    /// <param name="__instance">正在绘制的物品。</param>
    [HarmonyPostfix]
    [UsedImplicitly]
    static void Postfix(Thing __instance)
    {
        if (__instance.def.EverHaulable)
        {
            HaulExplicitlyPostingVisualizationDrawer.DrawForItem(__instance);
        }
    }
}

/// <summary>
/// 补丁：每帧的选择覆盖层绘制结束后，重置显式搬运可视化的本帧绘制记录。
/// </summary>
[HarmonyPatch(typeof(RimWorld.SelectionDrawer), "DrawSelectionOverlays")]
class SelectionDrawer_DrawSelectionOverlays_Patch
{
    /// <summary>
    /// 选择覆盖层绘制完成后清空本帧的绘制记录。
    /// </summary>
    [HarmonyPostfix]
    [UsedImplicitly]
    static void Postfix()
    {
        HaulExplicitlyPostingVisualizationDrawer.Clear();
    }
}