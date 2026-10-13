using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulExplicitly.AI;

/// <summary>
/// 让殖民者自动执行显式搬运的工作分配器：把地图上所有等待显式搬运的物品作为工作来源，
/// 并为其中合适的物品生成 <see cref="JobDriver_HaulExplicitly"/> 工作。
/// </summary>
public class WorkGiver_HaulExplicitly : WorkGiver_Scanner
{
    /// <summary>
    /// 前往目标时允许承受的最大危险等级：显式搬运是玩家明确下达的指令，允许穿过致命区域。
    /// </summary>
    /// <param name="pawn">执行工作的殖民者。</param>
    /// <returns>固定为 <see cref="Danger.Deadly"/>。</returns>
    public override Danger MaxPathDanger(Pawn pawn)
    {
        return Danger.Deadly;
    }

    /// <summary>
    /// 提供本工作分配器负责的全部物品，工作系统随后会逐个询问 <see cref="JobOnThing"/>。
    /// </summary>
    /// <param name="pawn">执行工作的殖民者。</param>
    /// <returns>该殖民者所在地图上所有等待显式搬运的物品。</returns>
    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        // 获取需要搬运的物品。如果成功获取，触发 WorkGiver_Scanner.HasJobOnThing -> WorkGiver_HaulExplicitly.JobOnThing
        return GameComponent_HaulExplicitly.GetManager(pawn).HaulableThings.ToList(); // 使用 ToList() 使返回的值为一个临时列表，从而不受外界修改干扰。防止：{pawn} threw exception in WorkGiver HaulExplicitly: System.InvalidOperationException: Collection was modified; enumeration operation may not execute.
    }

    /// <summary>
    /// 判断本工作分配器当前是否无事可做。
    /// </summary>
    /// <param name="pawn">执行工作的殖民者。</param>
    /// <param name="forced">是否为玩家强制指派。</param>
    /// <returns>没有任何等待显式搬运的物品时返回 <c>true</c>。</returns>
    public override bool ShouldSkip(Pawn pawn, bool forced = false)
    {
        return !PotentialWorkThingsGlobal(pawn).Any();
    }

    /// <summary>
    /// 为指定物品生成一次显式搬运工作：校验可达性与占用情况，计算本次愿意搬运的数量和可用的目的地格子，
    /// 并把数据 ID、目的地剩余承载能力、后续目的地格子分别编码进工作的目标队列。
    /// 若该物品所属的搬运工作已经全部搬完，则改为收尾：解除本次搬运中没搬走的剩余物品的「禁止搬运」标记。
    /// </summary>
    /// <param name="pawn">执行工作的殖民者。</param>
    /// <param name="t">要搬运的物品。</param>
    /// <param name="forced">是否为玩家强制指派。</param>
    /// <returns>生成的工作；当殖民者无法取得该物品、该记录已经搬完、目的地不可用或数量不足时返回 <c>null</c>。</returns>
    public override Job? JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        if (!CanGetThing(pawn, t, forced)) return null;
        if (!pawn.CanReserve(t)) return null;

        Data_DesignatorHaulExplicitly? data = GameComponent_HaulExplicitly.GetManager(t).DataWithItem(t);
        if (data == null) return null;
        
        var record = data.GetRecordWhichWithItem(t);
        if (record == null) return null; // 物品已从搬运记录中移除，跳过
        if (record.IsDone)
        {
            // 设定数量已经全部搬到目的地：收尾，把没搬走的剩余物品移出本次搬运工作
            // 并解除「禁止搬运」，让它们恢复自由搬运。
            // 还有殖民者正把手上的物品搬向目的地时收尾会失败，此时留到他们放下后（下一次扫描）再收尾，
            // 不能在这里把余量移出搬运工作，否则它会被留在「禁止搬运」状态。
            record.TryFinishHaulJob();
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

    /// <summary>
    /// 判断殖民者能否去搬运该物品：物品不能是已绑定账单的未完成品，
    /// 必须可以走到、可以占用，并且没有在燃烧。
    /// </summary>
    /// <param name="pawn">要执行搬运的殖民者。</param>
    /// <param name="t">目标物品。</param>
    /// <param name="forced">是否为玩家强制指派（影响占用检查）。</param>
    /// <returns>可以搬运返回 <c>true</c>。</returns>
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

    /// <summary>
    /// 计算殖民者本次愿意搬运的该物品数量：剩余待搬运数量、殖民者背包剩余空间与物品当前堆数三者取最小。
    /// </summary>
    /// <param name="p">要搬运的殖民者。</param>
    /// <param name="t">目标物品。</param>
    /// <param name="data">该物品所属的搬运数据。</param>
    /// <returns>本次愿意搬运的数量。</returns>
    public static int AmountPawnWantsToPickUp(Pawn p, Thing t, Data_DesignatorHaulExplicitly data)
    {
        return Mathf.Min(data.GetRecordWhichWithItem(t).GetNumRemainingToHaul(), p.carryTracker.AvailableStackSpace(t.def), t.stackCount);
    }
}