using HaulExplicitly.AI;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulExplicitly.Gizmo;

public class Command_Cancel_HaulExplicitly : Command
{
    private Thing thing;

    public Command_Cancel_HaulExplicitly(Thing t)
    {
        thing = t;
        defaultLabel = "HaulExplicitly.CancelHaulExplicitlyLabel".Translate();
        icon = ContentFinder<Texture2D>.Get("Buttons/DontHaulExplicitly");
        defaultDesc = "HaulExplicitly.CancelHaulExplicitlyDesc".Translate();
        hotKey = null;
    }

    public override void ProcessInput(Event ev)
    {
        base.ProcessInput(ev);
        Data_DesignatorHaulExplicitly? data = GameComponent_HaulExplicitly.GetManager(Find.CurrentMap).DataWithItem(thing);
        if (data == null) return;
        data.TryRemoveItem(thing, true);
        foreach (Pawn p in Find.CurrentMap.mapPawns.PawnsInFaction(Faction.OfPlayer).ListFullCopy())
        {
            var jobs = new List<Job>(p.jobs.jobQueue.AsEnumerable().Select(j => j.job));
            if (p.CurJob != null)
            {
                jobs.Add(p.CurJob);
            }
            foreach (var job in jobs)
            {
                if (job.def.driverClass == typeof(JobDriver_HaulExplicitly) && job.targetA.Thing == thing)
                {
                    p.jobs.EndCurrentOrQueuedJob(job, JobCondition.Incompletable);
                }
            }
        }
    }

    public static bool RelevantToThing(Thing t)
    {
        return GameComponent_HaulExplicitly.GetManager(t) != null ? GameComponent_HaulExplicitly.GetManager(t).DataWithItem(t) != null : false;
    }
}