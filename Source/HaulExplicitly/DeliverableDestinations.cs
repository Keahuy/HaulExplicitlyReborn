using HaulExplicitly.AI;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulExplicitly;

public class DeliverableDestinations
{
    public List<IntVec3> partialCells = [];

    public List<IntVec3> freeCells = [];

    private Func<IntVec3, float> _grader;

    public Data_DesignatorHaulExplicitly Data { get; }

    public InventoryRecord_DesignatorHaulExplicitly record { get; }

    private int _destinationsWithThisStackType;

    public List<int> PartialCellSpaceAvailable = [];

    private Thing _thing;

    private DeliverableDestinations(Thing item, Pawn carrier, Data_DesignatorHaulExplicitly data, Func<IntVec3, float> grader)
    {
        _grader = grader;
        Data = data;
        record = data.GetRecordWhichWithItem(item);
        Map map = data.Map;
        _thing = item;
        IntVec3 itemPos = (!item.SpawnedOrAnyParentSpawned) ? carrier.PositionHeld : item.PositionHeld;
        var traverseParms = TraverseParms.For(carrier);
        if (data.destinations != null)
        {
            foreach (IntVec3 cell in data.destinations)
            {
                List<Thing>? itemsInCell = Data_DesignatorHaulExplicitly.GetItemsIfValidItemSpot(map, cell);
                bool validDestination = itemsInCell != null;

                //see if this cell already has, or will have, an item of our item's stack type
                // (tests items in the cell, as well as reservations on the cell)
                bool cellIsSameStackType = false;
                if (validDestination)
                {
                    foreach (var unused in (itemsInCell ?? throw new InvalidOperationException()).Where(i => record.CanAdd(i)))
                    {
                        cellIsSameStackType = true;
                    }
                }

                Pawn claimant = map.reservationManager.FirstRespectedReserver(cell, carrier);
                if (claimant != null)
                {
                    List<Job> jobs =
                    [
                        ..claimant.jobs.jobQueue.Select(x => x.job),
                        claimant.jobs.curJob
                    ];
                    if (jobs.Any(job => job.def.driverClass == typeof(JobDriver_HaulExplicitly)
                                        && (job.targetB == cell || job.targetQueueB.Contains(cell))
                                        && record.CanAdd(job.targetA.Thing)))
                    {
                        cellIsSameStackType = true;
                    }
                }

                //finally, increment our counter of cells with our item's stack type
                if (cellIsSameStackType)
                {
                    _destinationsWithThisStackType++;
                }

                //check if cell is valid, reachable from item, unreserved, and pawn is allowed to go there
                bool reachable = map.reachability.CanReach(itemPos, cell, PathEndMode.ClosestTouch, traverseParms);
                if (!validDestination || !reachable || claimant != null || cell.IsForbidden(carrier)) continue;

                // oh, just item things
                if (itemsInCell is { Count: 0 })
                {
                    freeCells.Add(cell);
                }

                if (!itemsInCell.Any()) continue;
                var itemInCell = (itemsInCell ?? throw new InvalidOperationException()).Single();
                int spaceAvail = itemInCell.def.stackLimit - itemInCell.stackCount;
                if (!cellIsSameStackType || spaceAvail <= 0) continue;
                partialCells.Add(cell);
                PartialCellSpaceAvailable.Add(spaceAvail);
            }
        }
    }

    public static DeliverableDestinations For(Thing item, Pawn carrier, Data_DesignatorHaulExplicitly? data = null, Func<IntVec3, float>? grader = null)
    {
        if (data == null) //do the handholdy version of this function
        {
            data = GameComponent_HaulExplicitly.GetManager(item).DataWithItem(item);
            if (data == null) throw new ArgumentException();
        }

        return new DeliverableDestinations(item, carrier, data, grader ?? DefaultGrader);
    }

    public static float DefaultGrader(IntVec3 c)
    {
        return 0.0f;
    }

    public List<IntVec3> UsableDestinations()
    {
        int freeCellsWillUse = Math.Min(freeCells.Count, Math.Max(0, record.NumStacksWillUse - _destinationsWithThisStackType));
        List<IntVec3> result = new List<IntVec3>(partialCells);
        if (Enumerable.Any(freeCells))
        {
            result.AddRange(freeCells.OrderByDescending(_grader).Take(freeCellsWillUse));
        }

        return result;
    }

    public List<IntVec3> RequestSpaceForItemAmount(int amount)
    {
        List<IntVec3> usableDestinations = UsableDestinations();
        if (usableDestinations.Count == 0) return [];
        List<IntVec3> destinationsOrdered = new List<IntVec3>(ProximityOrdering(usableDestinations.RandomElement(), usableDestinations));
        int u; //number of destinations to use
        int destinationSpaceAvailable = 0;
        for (u = 0; u < destinationsOrdered.Count && destinationSpaceAvailable < amount; u++)
        {
            int i = partialCells.IndexOf(destinationsOrdered[u]);
            int space = (i == -1) ? _thing.def.stackLimit : PartialCellSpaceAvailable[i];
            destinationSpaceAvailable += space;
        }

        return [..destinationsOrdered.Take(u)];
    }

    
    public int FreeSpaceInCells(IEnumerable<IntVec3> cells)
    {
        // 搬运目标地点所有格子对被搬运物品的总剩余承载能力

        foreach (var c in cells)
        {
            if (!partialCells.Contains(c) && !freeCells.Contains(c))
            {
                throw new ArgumentException("Specified cells don't exist in DeliverableDestinations.");
            }

            var thingsAtCell = Data.Map.thingGrid.ThingsAt(c).ToList();
            if (!Enumerable.Any(thingsAtCell) || Enumerable.Any(thingsAtCell, t => t.def.category == ThingCategory.Plant))
            {
                return  _thing.def.stackLimit;
            }


            if (!Enumerable.Any(thingsAtCell, t => t.def.EverStorable(false))) continue;
            {
                var item = thingsAtCell.First(t => t.def.EverStorable(false)); 
                return  _thing.def.stackLimit - item.stackCount;
            }
        }

        return  _thing.def.stackLimit;
    }

    private static IEnumerable<IntVec3> ProximityOrdering(IntVec3 center, IEnumerable<IntVec3> cells)
    {
        return cells.OrderBy(c => Math.Abs(center.x - c.x) + Math.Abs(center.y - c.y));
    }
}