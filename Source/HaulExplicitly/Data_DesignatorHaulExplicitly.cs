using HaulExplicitly.Extension;
using RimWorld;
using UnityEngine;
using Verse;

namespace HaulExplicitly;

public class Data_DesignatorHaulExplicitly : IExposable
{
    private int _id;

    public int ID
    {
        get => _id;
        private set => _id = value;
    }

    private Map? _map; // 在构造器里为其初始化

    public Map Map => _map ??= Find.CurrentMap;

    public List<IntVec3>? destinations;

    public Vector3 cursor;

    public Vector3 center;

    public float visualizationRadius;

    public List<InventoryRecord_DesignatorHaulExplicitly> records = [];

    private readonly List<Thing> _items = [];

    public IReadOnlyList<Thing> Items => _items;

    public List<Thing> itemsWillForbidden = [];

    public Data_DesignatorHaulExplicitly()
    {
        // 如果不存在，报错：SaveableFromNode exception: System.MissingMethodException: Constructor on type 'HaulExplicitly.Data_DesignatorHaulExplicitly' not found.
    }

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
            if (!records.Any(record => record.TryAddItem(t)))
            {
                // 初始化 records 字段
                records.Add(new InventoryRecord_DesignatorHaulExplicitly(t, this));
            }
        }
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref _id, "dataId");
        Scribe_References.Look(ref _map, "map", true);
        Scribe_Collections.Look(ref records, "inventory", LookMode.Deep); // 🤔inventory 是历史遗留问题，为了不让订阅者报错，暂时不改了
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

    // 🤔有人报告了以下报错
    /*
     Exception while saving HaulExplicitly.GameComponent_HaulExplicitly: System.NullReferenceException: Object reference not set to an instance of an object
    [Ref ADF29D53]
        at HaulExplicitly.Data_DesignatorHaulExplicitly+<>c.<Clean>b__21_0 (Verse.Thing i) [0x00000] in <967157c04ed94c0a87fe87607aabb351>:0
    */
    // 所以我添加了这个 null 检查
    public void AddItem(Thing? thing)
    {
        if (thing != null)
        {
            _items.Add(thing);
        }
    }

    public int SwitchAutoForbidden(Thing t)
    {
        if (!_items.Contains(t)) return -1;
        InventoryRecord_DesignatorHaulExplicitly? ownerRecord = null;
        foreach (var record in records)
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

    public InventoryRecord_DesignatorHaulExplicitly GetRecordWhichWithItem(Thing t)
    {
        return records.FirstOrDefault(record => record.HasItem(t));
    }

    public bool TryRemoveItem(Thing t, bool playerCancelled = false, bool isToRemoveDestDrawing = false)
    {
        // 检查可行性
        if (!_items.Contains(t)) return false;

        // 移除 Record 中的记录
        InventoryRecord_DesignatorHaulExplicitly? ownerRecord = Enumerable.FirstOrDefault(records, record => record.HasItem(t));
        if (ownerRecord == null || !ownerRecord.TryRemoveItem(t, playerCancelled))
        {
            Log.Error("HaulExplicitly: Something went wrong.");
            return false;
        }

        // 移除 Data 中的记录
        _items.Remove(t);

        if (isToRemoveDestDrawing) return true;// 在 WorkGiver_HaulExplicitly 中，TryRemoveItem 用来在 HaulExplicitly 完成后不再显示 DeliverableDestinations 绘制的与目标地点的连线。这时不应该取消禁止搬运。 
        if (!t.GetIsInHaulExplicitlyDest()) // 如果 t 之前没有被 Designator_HaulExplicitly 搬运到指定地点
        {
            // 恢复可以被搬运，移除 Anchor 标记
            t.SetDontMoved(false);
        }

        return true;
    }

    public bool TryAddItemSplinter(Thing t)
    {
        // 如果一堆物品没有完全搬运，那么殖民者手上的那一小堆物品会变成新的一堆物品，有自己独特的ID
        // 该方法将这种物品加入 Record
        if (_items.Contains(t))
        {
            return false;
        }

        foreach (var record in records.Where(record => record.CanAdd(t)))
        {
            AddItem(t);
            record.TryAddItem(t, false);
            return true;
        }

        Log.Error("TryAddItemSplinter failed to find matching record for " + t);
        return false;
    }

    public void Clean()
    {
        var destroyedItems = new List<Thing>(_items.Where(i => i == null || i.Destroyed));
        foreach (var i in destroyedItems)
        {
            TryRemoveItem(i);
        }
    }

    public void ReloadItemsFromInventory()
    {
        _items.Clear();
        foreach (var t in records.SelectMany(r => r.items))
        {
            _items.Add(t);
        }
    }

    private void InventoryResetMerge()
    {
        foreach (InventoryRecord_DesignatorHaulExplicitly record in records)
        {
            record.ResetMerge();
        }
    }

    private bool IsPossibleItemDestination(IntVec3 c)
    {
        if (!c.InBounds(Map)
            || c.Fogged(Map)
            /*|| c.InNoZoneEdgeArea(Map)*/ // 🤔我认为没必要限制不能向地图边界搬运
            || c.GetTerrain(Map).passability == Traversability.Impassable
           ) return false; // 🤔目前不同搬运任务的目标地点可以重叠，但一些不同的物品显然是不能重叠的，能否设计一下自动识别空闲的格子，自动识别可堆叠的物品，当鼠标强制点击不空闲的格子时左上角提示
        return Map.thingGrid.ThingsAt(c).All(t => t.def.CanOverlapZones && t.def.passability != Traversability.Impassable /*&& !t.def.IsDoor 🤔也没必要限制不能运向门，左上角弹一个消息警告一下就好了，最好能在搬运前标记一下是哪个物品*/);
    }

    private IEnumerable<IntVec3> PossibleItemDestinationsAtCursor(Vector3 c) // 🤔此方法迭代了整个地图，真的有必要吗？
    {
        IntVec3 cursorCell = new IntVec3(c);
        var cardinals = new[] { IntVec3.North, IntVec3.South, IntVec3.East, IntVec3.West };
        HashSet<IntVec3> expended = []; // 只用来判定延伸的格子，从这些格子向外延伸4个格子作为可选目标去检验
        HashSet<IntVec3> available = []; // 可以放置物品的格子，会在接下来返回出去当作目的地，然后移至 expended
        if (IsPossibleItemDestination(cursorCell))
        {
            available.Add(cursorCell);
        }
        //🤔意思是鼠标所在格子不行，就不找周围的格子了？如果是，看看需不需要加一个再往外多找一圈格子的功能，自动找格子的话优先本房间内的格子

        while (available.Count > 0)
        {
            IntVec3 nearest = new IntVec3();
            float nearestDist = 100000000.0f;
            foreach (IntVec3 intVec3 in available)
            {
                float dist = (intVec3.ToVector3Shifted() - c).magnitude;
                if (!(dist < nearestDist)) continue; //🤔为什么 nearestDist 的初始值为 1
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
                || (thing.def.entityDefToBuild != null && thing.def.entityDefToBuild.passability != Traversability.Standable) // 🤔
                || (thing.def.surfaceType == SurfaceType.None && thing.def.passability != Traversability.Standable)) return null; // 🤔
            if (thing.def.EverStorable(false))
            {
                result.Add(thing);
            }
        }

        return result;
    }


    public bool TryMakeDestinations(Vector3 c, bool tryBeLazy = true)
    {
        if (tryBeLazy && c == cursor)
        {
            return destinations != null;
        }

        // 使用 HaulExplicitly 命令时
        cursor = c;
        int minStacks = records.Sum(record => record.NumStacksWillUse);
        // 🤔
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
                if (itemsInCell.Count != 1 || _items.Contains(item)) continue; // 🤔 对吗？itemsInCell有没有可能>1
                foreach (var record in records.Where(record => record.CanAdd(item) && item.stackCount != item.def.stackLimit))
                {
                    destinationsLocal.Add(cell);
                    record.AddMergeCell(item.stackCount);
                    break;
                }
            }

            if (destinationsLocal.Count < minStacks) continue;

            int stacks = records.Sum(record => record.NumStacksWillUse);
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