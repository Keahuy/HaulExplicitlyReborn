using HaulExplicitly.AI;
using RimWorld;
using Verse;
using Verse.AI;

namespace HaulExplicitly;

/// <summary>
/// 一次搬运的「目的地可交付性」分析结果：依据物品所属的搬运数据，把玩家指定的目的地格子划分为
/// 还能继续堆叠同种物品的未满格子（<see cref="partialCells"/>）与完全空着的格子（<see cref="freeCells"/>），
/// 并记录每个未满格子还能容纳多少物品。工作分配与工作执行都依赖它来决定把物品搬到哪里。
/// </summary>
public class DeliverableDestinations
{
    /// <summary>
    /// 已经有同种物品堆、但仍然可以继续堆叠的目的地格子。
    /// </summary>
    public List<IntVec3> partialCells = [];

    /// <summary>
    /// 完全空着的目的地格子。
    /// </summary>
    public List<IntVec3> freeCells = [];

    private Func<IntVec3, float> _grader;

    /// <summary>
    /// 被搬运物品所属的搬运数据。
    /// </summary>
    public Data_DesignatorHaulExplicitly Data { get; }

    /// <summary>
    /// 被搬运物品所属的搬运记录。
    /// </summary>
    public InventoryRecord_DesignatorHaulExplicitly record { get; }

    private int _destinationsWithThisStackType;

    /// <summary>
    /// 与 <see cref="partialCells"/> 一一对应的剩余可容纳数量。
    /// </summary>
    public List<int> PartialCellSpaceAvailable = [];

    private Thing _thing;

    /// <summary>
    /// 私有构造器：分析搬运数据中记录的全部目的地格子，筛出可用（存在、可达、未被占用、未被禁用）
    /// 的格子，并统计其中已有同种物品堆的格子数量。
    /// </summary>
    /// <param name="item">要搬运的物品。</param>
    /// <param name="carrier">执行搬运的殖民者，用于可达性与预留判定。</param>
    /// <param name="data">物品所属的搬运数据。</param>
    /// <param name="grader">给空格子评分以决定优先使用顺序的函数。</param>
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

    /// <summary>
    /// 创建目的地分析结果。
    /// </summary>
    /// <param name="item">要搬运的物品。</param>
    /// <param name="carrier">执行搬运的殖民者。</param>
    /// <param name="data">物品所属的搬运数据；为 <c>null</c> 时自动查找，查找失败会抛出异常。</param>
    /// <param name="grader">空格子的排序评分函数；为 <c>null</c> 时使用 <see cref="DefaultGrader"/>。</param>
    /// <returns>该物品本次可用的目的地分析结果。</returns>
    /// <exception cref="ArgumentException">当物品不属于任何搬运数据时抛出。</exception>
    public static DeliverableDestinations For(Thing item, Pawn carrier, Data_DesignatorHaulExplicitly? data = null, Func<IntVec3, float>? grader = null)
    {
        if (data == null) //do the handholdy version of this function
        {
            data = GameComponent_HaulExplicitly.GetManager(item).DataWithItem(item);
            if (data == null) throw new ArgumentException();
        }

        return new DeliverableDestinations(item, carrier, data, grader ?? DefaultGrader);
    }

    /// <summary>
    /// 默认的空格子评分函数：所有格子评分相同，即不改变原有顺序。
    /// </summary>
    /// <param name="c">待评分的格子。</param>
    /// <returns>固定返回 0。</returns>
    public static float DefaultGrader(IntVec3 c)
    {
        return 0.0f;
    }

    /// <summary>
    /// 汇总本次可用的目的地：包含全部未满格子，再加上按评分排序后取出的部分空格子；
    /// 空格子的取用数量不超过本次搬运还需要的新增堆数。
    /// </summary>
    /// <returns>可用的目的地格子列表。</returns>
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

    /// <summary>
    /// 从可用目的地中，按空间是否够用挑选出足以容纳指定数量物品的一组格子。
    /// </summary>
    /// <param name="amount">需要容纳的物品数量。</param>
    /// <returns>挑出的目的地格子列表；没有任何可用格子时返回空列表。</returns>
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

    
    /// <summary>
    /// 计算给定目的地格子集合对被搬运物品的总剩余承载能力。
    /// </summary>
    /// <param name="cells">要统计的目的地格子，必须属于本结果。</param>
    /// <returns>这些格子一共还能放下多少个被搬运物品。</returns>
    /// <exception cref="ArgumentException">当传入的格子既不是未满格子也不是空格子时抛出。</exception>
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

    /// <summary>
    /// 按曼哈顿距离把格子围绕指定中心由近到远排序。
    /// </summary>
    /// <param name="center">排序中心。</param>
    /// <param name="cells">待排序的格子。</param>
    /// <returns>由近到远排列的格子序列。</returns>
    private static IEnumerable<IntVec3> ProximityOrdering(IntVec3 center, IEnumerable<IntVec3> cells)
    {
        return cells.OrderBy(c => Math.Abs(center.x - c.x) + Math.Abs(center.y - c.y));
    }
}