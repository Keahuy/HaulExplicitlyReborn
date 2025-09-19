using HaulExplicitly.AI;
using RimWorld;
using Verse;

namespace HaulExplicitly;

public class InventoryRecord_DesignatorHaulExplicitly : IExposable
{
    private WeakReference? _parentData;

    public Data_DesignatorHaulExplicitly ParentData
    {
        get
        {
            if (_parentData != null) // 弱引用，Save and Load 存档后会被清理掉，所以要判断是否为空
            {
                return (Data_DesignatorHaulExplicitly)_parentData.Target;
            }

            // 调用 GameComponent_HaulExplicitly.GetManagers() 获得 mgr
            // 从 mgr 的 datas 中投影得到所有的 (Data_DesignatorHaulExplicitly)data 
            // 找出包含本 InventoryRecord_DesignatorHaulExplicitly 的 data *实例*
            foreach (var data in from mgr in GameComponent_HaulExplicitly.GetManagers() from data in mgr.datas.Values where data.Inventory.Contains(this) select data)
            {
                // 与其建立弱引用
                // 意义：防止互相引用导致GC不自动回收相关资源
                return (Data_DesignatorHaulExplicitly)(_parentData = new WeakReference(data)).Target;
            }

            // 我认为以上逻辑够完善了，不想再写一行没用的 return
            throw new Exception("HaulExplicitly: When load/create (WeakReference)_parentData at Class InventoryRecord_DesignatorHaulExplicitly, A error happen!");
        }
    }

    private ThingDef _itemDef;
    public ThingDef ItemDef { get; private set; }

    private int _mergeCapacity;
    public int MergeCapacity { get; private set; }

    private ThingDef _itemStuff;

    private int _numMergeStacksWillUse;
    public int NumMergeStacksWillUse { get; private set; }

    public int MovedQuantity = 0;
    public ThingDef ItemStuff { get; private set; }

    private ThingDef _miniDef;
    public ThingDef MiniDef { get; private set; }

    private int _selectedQuantity;
    public int SelectedQuantity { get; private set; }

    private int _playerSetQuantity = -1;

    public int SetQuantity
    {
        get => (_playerSetQuantity == -1) ? SelectedQuantity : _playerSetQuantity;
        set
        {
            if (value < 0 || value > SelectedQuantity)
                throw new ArgumentOutOfRangeException();
            _playerSetQuantity = value;
        }
    }

    public string Label
    {
        get { return GenLabel.ThingLabel(MiniDef ?? ItemDef, ItemStuff, SetQuantity).CapitalizeFirst(); }
    }


    // 需要占用的物品堆数 🤔不太明白
    public int NumStacksWillUse => StacksWorth(ItemDef, Math.Max(0, SetQuantity - MergeCapacity)) + NumMergeStacksWillUse;

    public List<Thing> Items = [];

    public void ExposeData()
    {
        Scribe_Collections.Look(ref Items, "items", LookMode.Reference);
        Scribe_Defs.Look(ref _itemDef, "itemDef");
        Scribe_Defs.Look(ref _itemStuff, "itemStuff");
        Scribe_Defs.Look(ref _miniDef, "minifiableDef");
        Scribe_Values.Look(ref _selectedQuantity, "selectedQuantity");
        Scribe_Values.Look(ref _playerSetQuantity, "setQuantity");
        Scribe_Values.Look(ref _mergeCapacity, "mergeCapacity");
        Scribe_Values.Look(ref _numMergeStacksWillUse, "numMergeStacksWillUse");
        Scribe_Values.Look(ref MovedQuantity, "movedQuantity");
    }

    public InventoryRecord_DesignatorHaulExplicitly()
    {
        // 防报错：SaveableFromNode exception: System.MissingMethodException: Constructor on type 'HaulExplicitly.HaulExplicitlyInventoryRecord' not found.   
    }

    public InventoryRecord_DesignatorHaulExplicitly(Thing initial, Data_DesignatorHaulExplicitly parentData)
    {
        _parentData = new WeakReference(parentData);
        Items.Add(initial);
        ItemDef = initial.def;
        ItemStuff = initial.Stuff;
        MiniDef = (initial as MinifiedThing)?.InnerThing.def;
        SelectedQuantity = initial.stackCount;
        ResetMerge();
    }

    public void ResetMerge()
    {
        MergeCapacity = 0;
        NumMergeStacksWillUse = 0;
    }

    public void AddMergeCell(int itemQuantity)
    {
        NumMergeStacksWillUse++;
        MergeCapacity += ItemDef.stackLimit - itemQuantity;
    }

    public static int StacksWorth(ThingDef thingDef, int quantity)
    {
        // 计算物品堆数
        return quantity / thingDef.stackLimit + (quantity % thingDef.stackLimit == 0 ? 0 : 1);
    }

    public bool CanAdd(Thing t)
    {
        return t.def.category == ThingCategory.Item
               && t.def == ItemDef
               && t.Stuff == ItemStuff
               && (t as MinifiedThing)?.InnerThing.def == MiniDef;
    }

    public bool HasItem(Thing t)
    {
        return Items.Contains(t);
    }

    public bool TryAddItem(Thing t, bool sideEffects = true)
    {
        if (!CanAdd(t)) return false;
        Items.Add(t);
        if (sideEffects)
        {
            SelectedQuantity += t.stackCount;
        }

        return true;
    }

    public bool TryRemoveItem(Thing t, bool playerCancelled = false)
    {
        bool r = Items.Remove(t);
        if (r && playerCancelled)
        {
            SelectedQuantity -= t.stackCount;
            _playerSetQuantity = Math.Min(_playerSetQuantity, SelectedQuantity);
        }

        return r;
    }

    public int RemainingToHaul()
    {
        var pawnsList = new List<Pawn>(ParentData.Map.mapPawns.PawnsInFaction(Faction.OfPlayer));
        int beingHauledNow = 0;
        foreach (Pawn p in pawnsList)
        {
            if (p.jobs.curJob == null)
            {
                continue;
            }

            if (p.jobs.curJob.def.driverClass == typeof(JobDriver_HaulExplicitly) && this == ((JobDriver_HaulExplicitly)p.jobs.curDriver).record)
            {
                beingHauledNow += p.jobs.curJob.count;
            }
        }

        return Math.Max(0, SetQuantity - (MovedQuantity + beingHauledNow));
    }
}