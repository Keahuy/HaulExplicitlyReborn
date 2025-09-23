using HarmonyLib;
using UnityEngine;
using Verse;

namespace HaulExplicitly;

public class HaulExplicitlyPostingVisualizationDrawer
{
    private static List<int> postings_drawn_this_frame = new();

    private static float alt
    {
        get { return AltitudeLayer.MetaOverlays.AltitudeFor(); }
    }

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

        if (postings_drawn_this_frame.Contains(data.ID))
            return;
        postings_drawn_this_frame.Add(data.ID);
        //draw circle
        GenDraw.DrawCircleOutline(circle_center, data.visualizationRadius);
    }

    public static void Clear()
    {
        postings_drawn_this_frame.Clear();
    }
}

[HarmonyPatch(typeof(Thing), "DrawExtraSelectionOverlays")]
class Thing_DrawExtraSelectionOverlays_Patch
{
    static void Postfix(Thing __instance)
    {
        if (__instance.def.EverHaulable)
        {
            HaulExplicitlyPostingVisualizationDrawer.DrawForItem(__instance);
        }
    }
}

[HarmonyPatch(typeof(RimWorld.SelectionDrawer), "DrawSelectionOverlays")]
class SelectionDrawer_DrawSelectionOverlays_Patch
{
    static void Postfix()
    {
        HaulExplicitlyPostingVisualizationDrawer.Clear();
    }
}