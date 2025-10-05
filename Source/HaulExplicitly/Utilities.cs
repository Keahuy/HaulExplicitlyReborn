using HaulExplicitly.AI;
using HaulExplicitly.Gizmo;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulExplicitly;

public static class Utilities
{
    public static Thing? GetFirstAlwaysHaulable(this IntVec3 c, Map map)
    {
        List<Thing> list = map.thingGrid.ThingsListAt(c);
        return Enumerable.FirstOrDefault(list, t => t.def.alwaysHaulable);
    }

    public static IEnumerable<Verse.Gizmo> GetHaulExplicitlyGizmos(Thing t)
    {
        if (t.def.EverHaulable)
        {
            yield return new Designator_HaulExplicitly();
        }
    }
    
    public static void UpdateLabelAndIcon(this Command_AutoForbiddenAfterHaulExplicitly instance ,Thing t)
    {
        var data = GameComponent_HaulExplicitly.GetManager(t).DataWithItem(t);
        if (data == null) return;
        if (data.itemsWillForbidden.Contains(t))
        {
            instance.defaultLabel = "HaulExplicitly.AutoForbiddenAfterHaulExplicitlyIsOnLabel".Translate();
            instance.icon = ContentFinder<Texture2D>.Get("Buttons/AutoForbiddenAfterHaulExplicitlyForbid");
        }
        else
        {
            instance.defaultLabel = "HaulExplicitly.AutoForbiddenAfterHaulExplicitlyIsOffLabel".Translate();
            instance.icon = ContentFinder<Texture2D>.Get("Buttons/AutoForbiddenAfterHaulExplicitlyNoForbid");
        }
    }
    
    public static void RemoveCurrentHaulExplicitlyJob(Thing t)
    {
        foreach (Pawn p in Find.CurrentMap.mapPawns.PawnsInFaction(Faction.OfPlayer).ListFullCopy())
        {
            var jobs = new List<Job>(p.jobs.jobQueue.AsEnumerable().Select(j => j.job));
            if (p.CurJob != null)
            {
                jobs.Add(p.CurJob);
            }
            foreach (var job in jobs.Where(job => job.def.driverClass == typeof(JobDriver_HaulExplicitly) && job.targetA.Thing == t))
            {
                p.jobs.EndCurrentOrQueuedJob(job, JobCondition.Incompletable);
            }
        }
    }
}