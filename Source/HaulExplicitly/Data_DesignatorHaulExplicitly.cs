using HaulExplicitly.Extension;
using RimWorld;
using UnityEngine;
using Verse;

namespace HaulExplicitly;

public class Data_DesignatorHaulExplicitly : IExposable
{
    private int _id;

    private Map? _map; // 在构造器里为其初始化

    public int ID
    {
        get => _id;
        private set => _id = value;
    }

    public Map Map
    {
        get => _map;
        private set => _map = value;
    }

    public List<IntVec3> Destinations;

    public Vector3 Cursor;

    public Vector3 Center;

    public float VisualizationRadius;

    // 于构造器中初始化
    public List<InventoryRecord_DesignatorHaulExplicitly> Inventory = [];

    public List<Thing> Items = [];

    public void ExposeData()
    {
        Scribe_Values.Look(ref _id, "postingId");
        Scribe_References.Look(ref _map, "map", true);
        Scribe_Collections.Look(ref Inventory, "inventory", LookMode.Deep);
        Scribe_Collections.Look(ref Destinations, "destinations", LookMode.Value);
        Scribe_Values.Look(ref Cursor, "cursor");
        Scribe_Values.Look(ref Center, "center");
        Scribe_Values.Look(ref VisualizationRadius, "visualizationRadius");

        if (Scribe.mode == LoadSaveMode.PostLoadInit)// 加载存档时
        {
            ReloadItemsFromInventory();
        }
    }

    public Data_DesignatorHaulExplicitly(IEnumerable<object> objects)
    {
        // 当新建一个 Data_DesignatorHaulExplicitly 时为其赋予一个独特的 ID 字段
        ID = GameComponent_HaulExplicitly.GetNewHaulExplicitlyDataID();
        // 初始化 Map 字段
        Map = Find.CurrentMap;
        foreach (object o in objects)
        {
            if (o is not Thing t || !t.def.EverHaulable)
            {
                continue;
            }

            Items.Add(t);
            // 尝试将物品加入一个可用的 Record ，如果没有可用的 Record 就新建一个
            if (!Inventory.Any(record => record.TryAddItem(t)))
            {
                // 初始化 Inventory 字段
                Inventory.Add(new InventoryRecord_DesignatorHaulExplicitly(t, this));
            }
        }
    }
    
    public InventoryRecord_DesignatorHaulExplicitly GetRecordWhichWithItem(Thing t)
    {
        return Inventory.First(record => record.HasItem(t));
    }
    
    public bool TryRemoveItem(Thing t, bool playerCancelled = false)
    {
        if (!Items.Contains(t)) return false;
        InventoryRecord_DesignatorHaulExplicitly? ownerRecord = null;
        foreach (var record in Inventory)
        {
            if (record.HasItem(t))
            {
                ownerRecord = record;
                break;
            }
        }

        if (ownerRecord == null || !ownerRecord.TryRemoveItem(t, playerCancelled))
        {
            Log.Error("Something went wrong hbhnoetb9ugob9g3b49.");
            return false;
        }

        Items.Remove(t);
        if (!t.GetIsInHaulExplicitlyDest())
        {
            t.SetDontMoved(false);
        }
        return true;
    }
    
    public bool TryAddItemSplinter(Thing t)
    {
        if (Items.Contains(t))
        {
            return false;
        }

        foreach (var record in Inventory)
        {
            if (record.CanAdd(t))
            {
                Items.Add(t);
                record.TryAddItem(t, false);
                return true;
            }
        }

        Log.Error("TryAddItemSplinter failed to find matching record for " + t);
        return false;
    }

    public void Clean()
    {
        var destroyedItems = new List<Thing>(Items.Where(i => i.Destroyed));
        foreach (var i in destroyedItems)
        {
            TryRemoveItem(i);
        }
    }
    public void ReloadItemsFromInventory()
    {
        Items = [];
        foreach (var t in Inventory.SelectMany(r => r.Items))
        {
            Items.Add(t);
        }
    }

    private void InventoryResetMerge()
    {
        foreach (InventoryRecord_DesignatorHaulExplicitly record in Inventory)
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

    private IEnumerable<IntVec3> PossibleItemDestinationsAtCursor(Vector3 cursor) // 🤔此方法迭代了整个地图，真的有必要吗？
    {
        IntVec3 cursorCell = new IntVec3(cursor);
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
            foreach (IntVec3 c in available)
            {
                float dist = (c.ToVector3Shifted() - cursor).magnitude;
                if (!(dist < nearestDist)) continue; //🤔为什么 nearestDist 的初始值为 1
                nearest = c;
                nearestDist = dist;
            }

            yield return nearest;
            available.Remove(nearest);
            expended.Add(nearest);

            foreach (IntVec3 dir in cardinals)
            {
                IntVec3 c = nearest + dir;
                if (expended.Contains(c) || available.Contains(c)) continue;
                var set = IsPossibleItemDestination(c) ? available : expended;
                set.Add(c);
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


    public bool TryMakeDestinations(Vector3 cursor, bool tryBeLazy = true)
    {
        if (tryBeLazy && cursor == Cursor)
        {
            return Destinations != null;
        }

        // 使用 HaulExplicitly 命令时
        Cursor = cursor;
        int minStacks = Inventory.Sum(record => record.NumStacksWillUse);
        // 🤔
        InventoryResetMerge();
        var destinations = new List<IntVec3>();
        foreach (var cell in PossibleItemDestinationsAtCursor(cursor)) // 此步从鼠标所在格子开始迭代了整个地图的格子用来判断可用的格子
        {
            List<Thing>? itemsInCell = GetItemsIfValidItemSpot(Map, cell);
            if (Map.reservationManager.IsReservedByAnyoneOf(cell, Faction.OfPlayer) // 如果该格子被预定了
                || itemsInCell == null) continue;
            if (itemsInCell.Count == 0)
            {
                destinations.Add(cell);
            }
            else
            {
                Thing item = itemsInCell.First();
                if (itemsInCell.Count != 1 || Items.Contains(item)) continue; // 🤔 对吗？itemsInCell有没有可能>1
                foreach (var record in Inventory.Where(record => record.CanAdd(item) && item.stackCount != item.def.stackLimit))
                {
                    destinations.Add(cell);
                    record.AddMergeCell(item.stackCount);
                    break;
                }
            }

            if (destinations.Count < minStacks) continue;

            int stacks = Inventory.Sum(record => record.NumStacksWillUse);
            if (destinations.Count < stacks) continue;
            //success operations
            Vector3 sum = destinations.Aggregate(Vector3.zero, (current, dest) => current + dest.ToVector3Shifted());
            Center = (1.0f / destinations.Count) * sum;
            VisualizationRadius = (float)Math.Sqrt(destinations.Count / Math.PI);
            Destinations = destinations;
            return true;
        }

        /*Destinations = null;*/ // 🤔
        return false;
    }
}