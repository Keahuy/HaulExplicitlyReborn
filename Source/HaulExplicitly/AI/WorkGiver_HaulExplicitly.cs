using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulExplicitly.AI;

public class WorkGiver_HaulExplicitly : WorkGiver_Scanner
{
    public override Danger MaxPathDanger(Pawn pawn)
    {
        return Danger.Deadly;
    }

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        // 获取需要搬运的物品。如果成功获取，触发 WorkGiver_Scanner.HasJobOnThing -> WorkGiver_HaulExplicitly.JobOnThing
        return GameComponent_HaulExplicitly.GetManager(pawn).HaulableThings.ToList();// 使用 ToList() 使返回的值为一个临时列表，从而不受外界修改干扰。防止：{pawn} threw exception in WorkGiver HaulExplicitly: System.InvalidOperationException: Collection was modified; enumeration operation may not execute.
    }

    public override bool ShouldSkip(Pawn pawn, bool forced = false)
    {
        return !PotentialWorkThingsGlobal(pawn).Any();
    }

    public override Job? JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (!CanGetThing(pawn, t, forced)) return null;
        if (!pawn.CanReserve(t)) return null;

        Data_DesignatorHaulExplicitly? data = GameComponent_HaulExplicitly.GetManager(t).DataWithItem(t);
        if (data == null) return null;

        if (data.GetRecordWhichWithItem(t).GetNumRemainingToHaul() == 0)
        {
            data.TryRemoveItem(t, true);
            return null;
        }

        //plan count and destinations

        int spaceRequest = AmountPawnWantsToPickUp(pawn, t, data); // 需要搬运的物品数
        var destinationsInfo = DeliverableDestinations.For(t, pawn, data);
        List<IntVec3> destinations = destinationsInfo.RequestSpaceForItemAmount(spaceRequest); // 目标地点当前可用格子
        int destinationSpaceAvailable = destinationsInfo.FreeSpaceInCells(destinations); // 目标地点所有格子对被搬运物品的总剩余承载能力
        var count = Math.Min(spaceRequest, destinationSpaceAvailable); // 可以向目标地点搬运 count 个物品
        if (count < 1) return null;
        if (destinations.Count == 0) return null; // 如果目标地点所有可用格子都被别的pawn占用了

        //make job
        JobDef jobDefOfHaulExplicitly = (JobDef)GenDefDatabase.GetDef(typeof(JobDef), "HaulExplicitly");
        Job job = new Job(jobDefOfHaulExplicitly, t, destinations.First())
        {
            count = count,
            targetQueueA = new List<LocalTargetInfo>([new IntVec3(data.ID, destinationSpaceAvailable, 0)]),
            targetQueueB = new List<LocalTargetInfo>(destinations.Skip(1).Take(destinations.Count - 1).Select(c => new LocalTargetInfo(c))),
            haulOpportunisticDuplicates = true
        };
        return job;
    }

    public static bool CanGetThing(Pawn pawn, Thing t, bool forced)
    {
        //tests based on AI.HaulAIUtility.PawnCanAutomaticallyHaulFast
        UnfinishedThing? unfinishedThing = t as UnfinishedThing;
        if (unfinishedThing is { BoundBill: not null }
            || !pawn.CanReach(t, PathEndMode.ClosestTouch, pawn.NormalMaxDanger(), mode: TraverseMode.ByPawn)
            || !pawn.CanReserve(t, 1, -1, null, forced))
        {
            return false;
        }

        if (t.IsBurning())
        {
            JobFailReason.Is("BurningLower".Translate());
            return false;
        }

        return true;
    }

    public static int AmountPawnWantsToPickUp(Pawn p, Thing t, Data_DesignatorHaulExplicitly data)
    {
        return Mathf.Min(data.GetRecordWhichWithItem(t).GetNumRemainingToHaul(), p.carryTracker.AvailableStackSpace(t.def), t.stackCount);
    }
}