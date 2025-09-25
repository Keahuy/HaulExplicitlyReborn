using HarmonyLib;
using HaulExplicitly.Extension;
using JetBrains.Annotations;
using UnityEngine;
using Verse;

namespace HaulExplicitly.Patch;

[HarmonyPatch(typeof(Designation), "DesignationDraw")]
class Designation_DesignationDraw_Patch
{
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