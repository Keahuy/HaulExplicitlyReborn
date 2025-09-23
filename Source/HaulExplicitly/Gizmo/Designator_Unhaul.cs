using HaulExplicitly.Extension;
using RimWorld;
using UnityEngine;
using Verse;

namespace HaulExplicitly.Gizmo;

public class Designator_Unhaul : Designator
{
    protected override DesignationDef Designation => HaulExplicitlyDefOf.HaulExplicitly_Unhaul;

    public Designator_Unhaul()
    {
        defaultLabel = "HaulExplicitly.SetUnhaulableLabel".Translate();
        icon = ContentFinder<Texture2D>.Get("Buttons/Unhaulable");
        defaultDesc = "HaulExplicitly.SetUnhaulableDesc".Translate();
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
        return t.def.alwaysHaulable && !t.GetDontMoved();
    }

    public override void DesignateThing(Thing? t)
    {
        if (t == null)
        {
            Log.Error("HaulExplicitly: The target things is null when use Unhaul command.");
            return;
        }

        t.SetDontMoved(true);
        /*// 为目标物品打上anchor标志
        Map.designationManager.AddDesignation(new Designation(t, Designation));*/
    }
}