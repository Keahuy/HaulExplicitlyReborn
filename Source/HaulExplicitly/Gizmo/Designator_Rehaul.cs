using HaulExplicitly.Extension;
using RimWorld;
using UnityEngine;
using Verse;

namespace HaulExplicitly.Gizmo;

public class Designator_Rehaul : Designator
{
    public Designator_Rehaul()
    {
        defaultLabel = "HaulExplicitly.SetHaulableLabel".Translate();
        icon = ContentFinder<Texture2D>.Get("Buttons/Haulable");
        defaultDesc = "HaulExplicitly.SetHaulableDesc".Translate();
        soundDragSustain = SoundDefOf.Designate_DragStandard;
        soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
        useMouseIcon = true;
        soundSucceeded = SoundDefOf.Designate_Haul;
        hotKey = null;
    }

    public override AcceptanceReport CanDesignateCell(IntVec3 c)
    {
        if (!c.InBounds(Map) || c.Fogged(Map))
        {
            return false;
        }

        Thing? firstAlwaysHaulable = c.GetFirstAlwaysHaulable(Map);
        if (firstAlwaysHaulable == null)
        {
            return false;
        }

        AcceptanceReport result = CanDesignateThing(firstAlwaysHaulable);
        if (!result.Accepted)
        {
            return result;
        }

        return true;
    }

    public override void DesignateSingleCell(IntVec3 c)
    {
        DesignateThing(c.GetFirstAlwaysHaulable(Map));
    }

    public override AcceptanceReport CanDesignateThing(Thing t)
    {
        return t.GetDontMoved();
    }

    public override void DesignateThing(Thing? t)
    {
        if (t == null)
        {
            Log.Error("HaulExplicitly: The target things is null when use Rehaul command.");
        }

        t?.SetDontMoved(false);
        t?.SetIsInHaulExplicitlyDest(false); // 
        /*Map.designationManager.TryRemoveDesignationOn(t, HaulExplicitlyDefOf.HaulExplicitly_Unhaul);*/
    }
}