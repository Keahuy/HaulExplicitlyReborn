using HaulExplicitly.Extension;
using RimWorld;
using UnityEngine;
using Verse;

namespace HaulExplicitly;

/// <summary>
/// 一次「显式搬运」指令的完整数据：记录被选中的物品清单、可用的目的地格子、
/// 以及绘制可视化提示所需的光标、圆心与半径。该数据会随存档保存，
/// 并由 <see cref="JobManager_DesignatorHaulExplicitly"/> 按 <see cref="ID"/> 统一管理。
/// </summary>
public class Data_DesignatorHaulExplicitly : IExposable
{
    private int _id;

    /// <summary>
    /// 本条数据的唯一标识，创建时由 <see cref="GameComponent_HaulExplicitly.GetNewHaulExplicitlyDataID"/> 分配。
    /// </summary>
    public int ID
    {
        get => _id;
        private set => _id = value;
    }

    private Map? _map; // 在构造器里为其初始化

    /// <summary>
    /// 数据所属的地图；读档后若引用为空则回退为当前地图。
    /// </summary>
    public Map Map => _map ??= Find.CurrentMap;

    /// <summary>
    /// 可以放置被搬运物品的候选格子；为 <c>null</c> 表示尚未计算。
    /// </summary>
    public List<IntVec3>? destinations;

    /// <summary>
    /// 玩家最近一次指定的光标位置，用于判断是否需要重新计算目的地。
    /// </summary>
    public Vector3 cursor;

    /// <summary>
    /// 所有目的地的几何中心，即可视化圆圈的圆心。
    /// </summary>
    public Vector3 center;

    /// <summary>
    /// 可视化圆圈的半径，按目的地数量估算得出。
    /// </summary>
    public float visualizationRadius;

    /// <summary>
    /// 按物品种类分组的搬运记录，每种可合并的物品对应一条 <see cref="InventoryRecord_DesignatorHaulExplicitly"/>。
    /// </summary>
    public List<InventoryRecord_DesignatorHaulExplicitly> inventory = [];

    private readonly List<Thing> _items = [];

    /// <summary>
    /// 本条数据涉及的全部物品（只读视图，实际存储在内部列表 <c>_items</c> 中）。
    /// </summary>
    public IReadOnlyList<Thing> Items => _items;

    /// <summary>
    /// 被标记为「搬运到位后自动禁用」的物品列表。
    /// </summary>
    public List<Thing> itemsWillForbidden = [];

    /// <summary>
    /// 供反序列化使用的无参构造器，不初始化任何业务字段。
    /// </summary>
    public Data_DesignatorHaulExplicitly()
    {
        // 如果不存在，报错：SaveableFromNode exception: System.MissingMethodException: Constructor on type 'HaulExplicitly.Data_DesignatorHaulExplicitly' not found.
    }

    /// <summary>
    /// 依据玩家当前选中的对象创建一条新的搬运数据：分配唯一 ID、绑定当前地图，
    /// 并把其中所有可搬运的物品归入（必要时新建）对应的搬运记录。
    /// </summary>
    /// <param name="objects">玩家选中的对象，其中只有可搬运的 <see cref="Thing"/> 会被采纳。</param>
    public Data_DesignatorHaulExplicitly(IEnumerable<object> objects)
    {
        // 当新建一个 Data_DesignatorHaulExplicitly 时为其赋予一个独特的 ID 字段
        ID = GameComponent_HaulExplicitly.GetNewHaulExplicitlyDataID();
        // 初始化 Map 字段
        _map = Find.CurrentMap;
        foreach (object o in objects)
        {
            if (o is not Thing t || !t.def.EverHaulable)
            {
                continue;
            }

            AddItem(t);
            // 尝试将物品加入一个可用的 Record ，如果没有可用的 Record 就新建一个
            if (!inventory.Any(record => record.TryAddItem(t)))
            {
                // 初始化 inventory 字段
                inventory.Add(new InventoryRecord_DesignatorHaulExplicitly(t, this));
            }
        }
    }

    /// <summary>
    /// 存读档钩子：读写 ID、地图、搬运记录、目的地、可视化参数与自动禁用列表；
    /// 读档完成后依据搬运记录重建物品列表。
    /// </summary>
    public void ExposeData()
    {
        Scribe_Values.Look(ref _id, "dataId");
        Scribe_References.Look(ref _map, "map", true);
        Scribe_Collections.Look(ref inventory, "inventory", LookMode.Deep); // inventory 是历史遗留问题，为了不让订阅者报错，暂时不改了
        Scribe_Collections.Look(ref destinations, "destinations", LookMode.Value);
        Scribe_Values.Look(ref cursor, "cursor");
        Scribe_Values.Look(ref center, "center");
        Scribe_Values.Look(ref visualizationRadius, "visualizationRadius");
        Scribe_Collections.Look(ref itemsWillForbidden, "itemsWillForbidden", LookMode.Reference);

        if (Scribe.mode == LoadSaveMode.PostLoadInit) // 加载存档时
        {
            ReloadItemsFromInventory();
        }
    }

    // 有人报告了以下报错
    /*
     Exception while saving HaulExplicitly.GameComponent_HaulExplicitly: System.NullReferenceException: Object reference not set to an instance of an object
    [Ref ADF29D53]
        at HaulExplicitly.Data_DesignatorHaulExplicitly+<>c.<Clean>b__21_0 (Verse.Thing i) [0x00000] in <967157c04ed94c0a87fe87607aabb351>:0
    */
    // 所以我添加了这个 null 检查
    /// <summary>
    /// 把一个物品加入本条数据的物品列表；<paramref name="thing"/> 为 <c>null</c> 时直接忽略。
    /// </summary>
    /// <param name="thing">要加入的物品，允许为 <c>null</c>。</param>
    /// <remarks>这里的非空判断用于修复历史上保存存档时因物品为空而抛出的 NullReferenceException。</remarks>
    public void AddItem(Thing? thing)
    {
        if (thing != null)
        {
            _items.Add(thing);
        }
    }

    /// <summary>
    /// 切换指定物品的「搬运到位后自动禁用」状态。
    /// </summary>
    /// <param name="t">要切换状态的物品，必须已属于本条数据。</param>
    /// <returns>1 表示已加入自动禁用列表，0 表示已移出，-1 表示该物品不属于本条数据或找不到其所属记录（此时会记录错误日志）。</returns>
    public int SwitchAutoForbidden(Thing t)
    {
        if (!_items.Contains(t)) return -1;
        InventoryRecord_DesignatorHaulExplicitly? ownerRecord = null;
        foreach (var record in inventory)
        {
            if (record.HasItem(t))
            {
                ownerRecord = record;
                break;
            }
        }

        if (ownerRecord == null)
        {
            Log.Error("Something went wrong hbhnoetb9ugob9g3b49.");
            return -1;
        }

        if (itemsWillForbidden.Contains(t))
        {
            itemsWillForbidden.Remove(t);
            return 0;
        }
        else
        {
            itemsWillForbidden.Add(t);
            return 1;
        }
    }

    /// <summary>
    /// 找出包含指定物品的搬运记录。
    /// </summary>
    /// <param name="t">要查找的物品。</param>
    /// <returns>包含该物品的记录；找不到时返回 <c>null</c>。</returns>
    public InventoryRecord_DesignatorHaulExplicitly GetRecordWhichWithItem(Thing t)
    {
        return inventory.FirstOrDefault(record => record.HasItem(t));
    }

    /// <summary>
    /// 把物品从本条数据及其所属的搬运记录中移除，并按需恢复该物品的可搬运状态。
    /// </summary>
    /// <param name="t">要移除的物品。</param>
    /// <param name="playerCancelled">是否由玩家主动取消（为 <c>true</c> 时会同步扣减该记录的设定数量）。</param>
    /// <param name="isToRemoveDestDrawing">
    /// 仅停止绘制「物品到目的地」的连线，而不解除「不要自动搬走」标记；
    /// 供搬运完成时调用。
    /// </param>
    /// <returns>成功移除返回 <c>true</c>；物品不在本条数据中、已不在世上或移除记录失败时返回 <c>false</c>。</returns>
    public bool TryRemoveItem(Thing t, bool playerCancelled = false, bool isToRemoveDestDrawing = false)
    {
        // 检查可行性
        if (!_items.Contains(t)) return false;
        if (!t.Spawned) return false; // record 里为了避免报错已经先一步把 t 移除了，所以找不到ownerRecord。这里加入检查避免去找ownerRecord

        // 移除 Record 中的记录
        InventoryRecord_DesignatorHaulExplicitly? ownerRecord = Enumerable.FirstOrDefault(inventory, record => record.HasItem(t));
        if (ownerRecord == null)
        {
            return false;
        }

        if (!ownerRecord.TryRemoveItem(t, playerCancelled))
        {
            Log.Error("HaulExplicitly: Something went wrong.");
            return false;
        }

        // 移除 Data 中的记录
        _items.Remove(t);

        if (isToRemoveDestDrawing) return true; // 在 WorkGiver_HaulExplicitly 中，TryRemoveItem 用来在 HaulExplicitly 完成后不再显示 DeliverableDestinations 绘制的与目标地点的连线。这时不应该取消禁止搬运。 
        if (!t.GetIsInHaulExplicitlyDest()) // 如果 t 之前没有被 Designator_HaulExplicitly 搬运到指定地点
        {
            // 恢复可以被搬运，移除 Anchor 标记
            t.SetDontMoved(false);
        }

        return true;
    }

    /// <summary>
    /// 把一「小堆」物品归入某条可接受它的搬运记录。
    /// </summary>
    /// <param name="t">要加入的物品。搬运过程中被殖民者拿起的那部分物品会分裂成新的一堆并拥有自己的 ID，本方法正是把它们接回原有记录。</param>
    /// <returns>加入成功返回 <c>true</c>；物品已存在或找不到匹配的记录时返回 <c>false</c>。</returns>
    public bool TryAddItemSplinter(Thing t)
    {
        // 如果一堆物品没有完全搬运，那么殖民者手上的那一小堆物品会变成新的一堆物品，有自己独特的ID
        // 该方法将这种物品加入 Record
        if (_items.Contains(t))
        {
            return false;
        }

        foreach (var record in inventory.Where(record => record.CanAdd(t)))
        {
            AddItem(t);
            record.TryAddItem(t, false);
            return true;
        }

        Log.Error("TryAddItemSplinter failed to find matching record for " + t);
        return false;
    }

    /// <summary>
    /// 清理本条数据：移除所有已被摧毁的物品所占用的记录（在保存存档前调用）。
    /// </summary>
    public void Clean()
    {
        var destroyedItems = new List<Thing>(_items.Where(i => i.Destroyed));
        foreach (Thing i in destroyedItems)
        {
            TryRemoveItem(i);
        }
    }

    /// <summary>
    /// 依据搬运记录重建物品列表（读档完成后调用）。
    /// </summary>
    public void ReloadItemsFromInventory()
    {
        _items.Clear();
        foreach (var t in inventory.SelectMany(r => r.items))
        {
            _items.Add(t);
        }
    }

    /// <summary>
    /// 重置全部搬运记录的合并统计（每次重新计算目的地之前调用）。
    /// </summary>
    private void InventoryResetMerge()
    {
        foreach (InventoryRecord_DesignatorHaulExplicitly record in inventory)
        {
            record.ResetMerge();
        }
    }

    /// <summary>
    /// 判断某个格子是否可以作为物品的目的地：必须在边界内、不在迷雾中、地形可通行，
    /// 且格子上已有的东西都允许与储存区重叠。
    /// </summary>
    /// <param name="c">待检查的格子。</param>
    /// <returns>可以作为目的地返回 <c>true</c>。</returns>
    private bool IsPossibleItemDestination(IntVec3 c)
    {
        if (!c.InBounds(Map)
            || c.Fogged(Map)
            || c.GetTerrain(Map).passability == Traversability.Impassable
           ) return false;
        return Map.thingGrid.ThingsAt(c).All(t => t.def.CanOverlapZones && t.def.passability != Traversability.Impassable);
    }

    /// <summary>
    /// 以光标所在格子为起点，按距离由近到远向外扩展，惰性地产出所有可用且相互连通的目的地格子。
    /// </summary>
    /// <param name="c">光标位置（世界坐标）。</param>
    /// <returns>可作为目的地的格子序列，按距光标由近到远排列。</returns>
    private IEnumerable<IntVec3> PossibleItemDestinationsAtCursor(Vector3 c)
    {
        IntVec3 cursorCell = new IntVec3(c);
        var cardinals = new[] { IntVec3.North, IntVec3.South, IntVec3.East, IntVec3.West };
        HashSet<IntVec3> expended = []; // 只用来判定延伸的格子，从这些格子向外延伸4个格子作为可选目标去检验
        HashSet<IntVec3> available = []; // 可以放置物品的格子，会在接下来返回出去当作目的地，然后移至 expended
        if (IsPossibleItemDestination(cursorCell))
        {
            available.Add(cursorCell);
        }

        while (available.Count > 0)
        {
            IntVec3 nearest = new IntVec3();
            float nearestDist = 100000000.0f;
            foreach (IntVec3 intVec3 in available)
            {
                float dist = (intVec3.ToVector3Shifted() - c).magnitude;
                if (!(dist < nearestDist)) continue;
                nearest = intVec3;
                nearestDist = dist;
            }

            yield return nearest;
            available.Remove(nearest);
            expended.Add(nearest);

            foreach (IntVec3 dir in cardinals)
            {
                IntVec3 intVec3 = nearest + dir;
                if (expended.Contains(intVec3) || available.Contains(intVec3)) continue;
                var set = IsPossibleItemDestination(intVec3) ? available : expended;
                set.Add(intVec3);
            }
        }
    }

    /// <summary>
    /// 检查指定格子是否适合作为物品存放点，并列出该格子上已有的可储存物品。
    /// </summary>
    /// <param name="map">格子所在的地图。</param>
    /// <param name="cell">待检查的格子。</param>
    /// <returns>
    /// 该格子上的可储存物品列表（可能为空列表表示空格子）；
    /// 若格子越界、在迷雾中、位于区域边缘、地形不可通行、有静态火焰或存在不可重叠的物体，则返回 <c>null</c>。
    /// </returns>
    /// <remarks>判定逻辑参考原版 <c>Designation_ZoneAddStockpile.CanDesignateCell</c> 与 <c>StoreUtility.IsGoodStoreCell</c>。</remarks>
    public static List<Thing>? GetItemsIfValidItemSpot(Map map, IntVec3 cell)
    {
        //references used for this function (referenced during Rimworld 0.19):
        // Designation_ZoneAddStockpile.CanDesignateCell
        // StoreUtility.IsGoodStoreCell
        // 🤔
        var result = new List<Thing>();
        if (!cell.InBounds(map)
            || cell.Fogged(map)
            || cell.InNoZoneEdgeArea(map)
            || cell.GetTerrain(map).passability == Traversability.Impassable
            || cell.ContainsStaticFire(map)) return null;
        List<Thing> things = map.thingGrid.ThingsListAt(cell);
        foreach (Thing thing in things)
        {
            if (!thing.def.CanOverlapZones // thing 上面不能画储存区、种植区之类的
                || (thing.def.entityDefToBuild != null && thing.def.entityDefToBuild.passability != Traversability.Standable)
                || (thing.def.surfaceType == SurfaceType.None && thing.def.passability != Traversability.Standable)) return null;
            if (thing.def.EverStorable(false))
            {
                result.Add(thing);
            }
        }

        return result;
    }


    /// <summary>
    /// 依据待搬运物品所需的堆数，从光标位置开始计算出足够的目的地格子集合，
    /// 并更新可视化的圆心与半径。
    /// </summary>
    /// <param name="c">当前光标位置（世界坐标）。</param>
    /// <param name="tryBeLazy">为 <c>true</c> 且光标未移动时，直接复用上一次的计算结果。</param>
    /// <returns>成功算出目的地返回 <c>true</c>；可用格子不足时返回 <c>false</c>。</returns>
    public bool TryMakeDestinations(Vector3 c, bool tryBeLazy = true)
    {
        if (tryBeLazy && c == cursor)
        {
            return destinations != null;
        }

        // 使用 HaulExplicitly 命令时
        cursor = c;
        int minStacks = inventory.Sum(record => record.NumStacksWillUse);
        InventoryResetMerge();
        var destinationsLocal = new List<IntVec3>();
        foreach (var cell in PossibleItemDestinationsAtCursor(c)) // 此步从鼠标所在格子开始迭代了整个地图的格子用来判断可用的格子
        {
            List<Thing>? itemsInCell = GetItemsIfValidItemSpot(Map, cell);
            if (Map.reservationManager.IsReservedByAnyoneOf(cell, Faction.OfPlayer) // 如果该格子被预定了
                || itemsInCell == null) continue;
            if (itemsInCell.Count == 0)
            {
                destinationsLocal.Add(cell);
            }
            else
            {
                Thing item = itemsInCell.First();
                if (itemsInCell.Count != 1 || _items.Contains(item)) continue;
                foreach (var record in inventory.Where(record => record.CanAdd(item) && item.stackCount != item.def.stackLimit))
                {
                    destinationsLocal.Add(cell);
                    record.AddMergeCell(item.stackCount);
                    break;
                }
            }

            if (destinationsLocal.Count < minStacks) continue;

            int stacks = inventory.Sum(record => record.NumStacksWillUse);
            if (destinationsLocal.Count < stacks) continue;
            //success operations
            Vector3 sum = destinationsLocal.Aggregate(Vector3.zero, (current, dest) => current + dest.ToVector3Shifted());
            center = (1.0f / destinationsLocal.Count) * sum;
            visualizationRadius = (float)Math.Sqrt(destinationsLocal.Count / Math.PI);
            destinations = destinationsLocal;
            return true;
        }

        return false;
    }
}