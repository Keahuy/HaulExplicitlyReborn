using HaulExplicitly.Extension;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulExplicitly.AI;

/// <summary>
/// 显式搬运工作用到的自定义 Toil（工作步骤）：拾取物品、在目的地放下物品。
/// </summary>
public class Toils_HaulExplicitly
{
    /// <summary>
    /// 创建「拾取物品」步骤：按工作剩余数量与殖民者背包剩余空间拿起目标物品的一部分，
    /// 把拿起的这一堆登记回搬运记录；若本工作允许顺手搬运同类物品
    /// （<c>haulOpportunisticDuplicates</c>），还会寻找附近可搬运的同类物品，
    /// 为它预约目的地并把数量与目的地并入当前工作。
    /// </summary>
    /// <param name="haulxItemInd">目标队列中指向待搬运物品的索引。</param>
    /// <returns>可以加入工作流程的 Toil。</returns>
    public static Toil PickUpThing(TargetIndex haulxItemInd)
    {
        Toil toil = ToilMaker.MakeToil();
        toil.initAction = delegate
        {
            Pawn actor = toil.actor;
            Job job = actor.CurJob;
            Thing target = job.GetTarget(haulxItemInd).Thing;
            if (Toils_Haul.ErrorCheckForCarry(actor, target)) return;

            Thing carriedItem = actor.carryTracker.CarriedThing;
            int targetInitialStackcount = target.stackCount;
            int countToPickUp = Mathf.Min(job.count - (carriedItem?.stackCount ?? 0), actor.carryTracker.AvailableStackSpace(target.def), targetInitialStackcount);
            if (countToPickUp <= 0) throw new Exception("PickUpThing countToPickUp = " + countToPickUp);

            //pick up
            int countPickedUp = actor.carryTracker.TryStartCarry(target, countToPickUp);
            if (countPickedUp < targetInitialStackcount)
            {
                actor.Map.reservationManager.Release(target, actor, job);
            }

            carriedItem = actor.carryTracker.CarriedThing;
            job.SetTarget(haulxItemInd, carriedItem);
            actor.records.Increment(RecordDefOf.ThingsHauled);

            //register the carried item (into the HaulExplicitly job)
            var driver = (JobDriver_HaulExplicitly)actor.jobs.curDriver;
            driver.Data.TryAddItemSplinter(carriedItem);

            //pick up next available item in job?
            if (!actor.CurJob.haulOpportunisticDuplicates) return;
            Thing? prospect = null;
            int bestDist = 999;
            foreach (Thing item in driver.record.items.Where(i => i != null && i.Spawned && WorkGiver_HaulExplicitly.CanGetThing(actor, i, false)))
            {
                IntVec3 offset = item.Position - actor.Position;
                int dist = Math.Abs(offset.x) + Math.Abs(offset.z);
                if (dist < bestDist && dist < 7)
                {
                    prospect = item;
                    bestDist = dist;
                }
            }

            if (prospect == null) return;
            int spaceRequest = WorkGiver_HaulExplicitly.AmountPawnWantsToPickUp(actor, prospect, driver.Data);
            if (spaceRequest == 0) return;
            var destInfo = DeliverableDestinations.For(prospect, actor, driver.Data);
            List<IntVec3> destinations = destInfo.RequestSpaceForItemAmount(Math.Max(0, spaceRequest - driver.DestinationSpaceAvailable));
            int newDestinationSpace = destInfo.FreeSpaceInCells(destinations);
            var count = Math.Min(spaceRequest, driver.DestinationSpaceAvailable + newDestinationSpace);
            if (count < 1)
                return;

            //commit to it
            actor.Reserve(prospect, job);
            job.SetTarget(haulxItemInd, prospect);
            job.SetTarget(TargetIndex.C, prospect.Position);
            foreach (var dest in destinations)
            {
                actor.Reserve(dest, job);
                job.targetQueueB.Add(dest);
            }

            job.count += count;
        };
        return toil;
    }

    /// <summary>
    /// 创建「在目的地放下物品」步骤：把手上的物品放到当前目的地格上，累加已搬运数量、
    /// 标记该物品已到达显式搬运目的地，并按需将其禁用；若一次放不完，则改用队列中的下一个
    /// 目的地格子继续放置；已无处可放时把物品搬到一旁，实在不行则销毁并记录错误。
    /// </summary>
    /// <param name="destInd">目的地（或目的地队列）在目标队列中的索引。</param>
    /// <param name="nextToilIfNotDonePlacing">未能一次放完时跳回的 Toil，通常就是搬运步骤本身；为 <c>null</c> 时不重试。</param>
    /// <returns>可以加入工作流程的 Toil。</returns>
    public static Toil PlaceHauledThingAtDest(TargetIndex destInd, Toil? nextToilIfNotDonePlacing)
    {
        Toil toil = ToilMaker.MakeToil();
        toil.initAction = delegate
        {
            //get all the vars
            Pawn actor = toil.actor;
            Thing carriedItem = actor.carryTracker.CarriedThing;
            if (carriedItem == null)
            {
                Log.Error($" HaulExplicitly: {actor} tried to place hauled thing in cell but is not hauling anything.");
                return;
            }

            int carryBeforeCount = carriedItem.stackCount;
            Job job = actor.CurJob;
            var driver = (JobDriver_HaulExplicitly)actor.jobs.curDriver;
            IntVec3 destination = job.GetTarget(destInd).Cell;

            //put it down now
            bool done = actor.carryTracker.TryDropCarriedThing(destination, ThingPlaceMode.Direct, out _);

            if (done)
            {
                job.count = 0;
                driver.record.MovedQuantity += carryBeforeCount;
                carriedItem.SetIsInHaulExplicitlyDest(true);
                driver.Data.TryRemoveItem(carriedItem);
                if (driver.Data.itemsWillForbidden.Contains(carriedItem))
                {
                    if (carriedItem is ThingWithComps itemWillForbidden && itemWillForbidden.GetComp<CompForbiddable>() != null)
                    {
                        carriedItem.SetForbidden(true);
                    }
                }

                if (carriedItem.Spawned && !carriedItem.GetDontMoved()) // 如果 carriedItem 没有被合并进已有物品堆，并且没有被禁止搬运
                {
                    carriedItem.SetDontMoved(true);
                }

                TryFinishHaulJob(driver);
            }
            else
            {
                var placedCount = carryBeforeCount - carriedItem.stackCount;
                job.count -= placedCount;
                driver.record.MovedQuantity += placedCount;
                var destQueue = job.GetTargetQueue(destInd);
                if (nextToilIfNotDonePlacing != null && destQueue.Count != 0)
                {
                    //put the remainder in the next queued cell
                    job.SetTarget(destInd, destQueue[0]);
                    destQueue.RemoveAt(0);
                    driver.JumpToToil(nextToilIfNotDonePlacing);
                }
                else
                {
                    //can't continue the job normally
                    job.count = 0;
                    Job haulAsideJob = HaulAIUtility.HaulAsideJobFor(actor, carriedItem);
                    if (haulAsideJob != null)
                    {
                        actor.jobs.StartJob(haulAsideJob);
                    }
                    else
                    {
                        Log.Error("Incomplete explicit haul for " + actor
                                                                  + ": Could not find anywhere to put "
                                                                  + carriedItem + " near " + actor.Position
                                                                  + ". Destroying. This should never happen!");
                        carriedItem.Destroy();
                    }
                }
            }
        };
        return toil;
    }

    /// <summary>
    /// 在物品成功放到目的地之后检查本次搬运是否已全部完成：是则收尾，
    /// 把没搬走的剩余物品移出本次搬运工作并解除其「禁止搬运」标记。
    /// </summary>
    /// <param name="driver">刚放下物品的显式搬运工作驱动器。</param>
    /// <remarks>
    /// 物品无法正常放下（目的地失效、只能搬到一旁）时不调用本方法：
    /// 此时设定数量尚未搬完，仍应由工作分配器继续尝试搬运；
    /// 收尾工作也可能由 <c>WorkGiver_HaulExplicitly</c> 在无人搬运该记录时触发。
    /// </remarks>
    private static void TryFinishHaulJob(JobDriver_HaulExplicitly driver)
    {
        driver.record.TryFinishHaulJob();
    }
}