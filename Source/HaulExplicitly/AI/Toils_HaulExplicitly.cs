using HaulExplicitly.Extension;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulExplicitly.AI;

public class Toils_HaulExplicitly
{
    public static Toil PickUpThing(TargetIndex haulxItemInd, Toil nextToilIfBeingOpportunistic)
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
            foreach (Thing item in driver.record.Items.Where(i => i != null && i.Spawned && WorkGiver_HaulExplicitly.CanGetThing(actor, i, false)))
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
            // driver.Init(); //this fixes problems
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
}