using HaulExplicitly.Extension;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

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

        t.SetDontMoved(true);// 标记为禁止搬运
        
        // 取消与该物品有关的工作
        foreach (Pawn p in Find.CurrentMap.mapPawns.PawnsInFaction(Faction.OfPlayer).ListFullCopy())
        {
            var jobs = new List<Job>(p.jobs.jobQueue.AsEnumerable().Select(j => j.job));
            if (p.CurJob != null)
            {
                jobs.Add(p.CurJob);
            }

            foreach (var job in jobs)
            {
                if (job.targetA.Thing == t)
                {
                    p.jobs.EndCurrentOrQueuedJob(job, JobCondition.Incompletable);
                }
            }
        }
    }
}