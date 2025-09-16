using HarmonyLib;
using HaulExplicitly.Extension;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse;

namespace HaulExplicitly.Patch;

[HarmonyPatch(typeof(Designation), "DesignationDraw")]
class Designation_DesignationDraw_Patch
{
    [HarmonyPostfix]
    [UsedImplicitly]
    static void MakeMarkToDistinguishHaulableOrNot(Designation __instance)
    {
        Thing t = __instance.target.Thing;
        Vector3 pos = t.DrawPos;
        pos.x += t.RotatedSize.x * 0.3f;
        pos.y = AltitudeLayer.MetaOverlays.AltitudeFor() + 0.155f;
        string? matpath;
        Mesh mesh;

        if (t.Spawned)
        {
            if (t.GetDontMoved())
            {
                matpath = "Overlay/Anchor";
                pos.z -= t.RotatedSize.z * 0.3f;
                mesh = MeshPool.plane03;
                Graphics.DrawMesh(mesh, pos, Quaternion.identity, MaterialPool.MatFrom(matpath, ShaderDatabase.MetaOverlay), 0);
            }
        }

        /*if (!t.GetDontMoved())
        {
            matpath = "Overlay/Move";
            pos.z += t.RotatedSize.z * 0.3f;
            mesh = MeshPool.plane03;
        }*/
        
    }
}