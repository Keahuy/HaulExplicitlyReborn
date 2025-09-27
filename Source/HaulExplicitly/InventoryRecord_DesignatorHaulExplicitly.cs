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
            foreach (var data in from mgr in GameComponent_HaulExplicitly.GetManagers() from data in mgr.datas.Values where data.inventory.Contains(this) select data)
            {
                // 与其建立弱引用
                // 意义：防止互相引用导致GC不自动回收相关资源
                return (Data_DesignatorHaulExplicitly)(_parentData = new WeakReference(data)).Target;
            }

            // 我认为以上逻辑够完善了，不想再写一行没用的 return
            throw new Exception("HaulExplicitly: When load/create (WeakReference)_parentData at Class InventoryRecord_DesignatorHaulExplicitly, A error happen!");
        }
    }

    private ThingDef? _itemDef;

    public ThingDef ItemDef
    {
        get
        {
            _itemDef ??= Items.First().def;

            return _itemDef;
        }
        private set => _itemDef = value;
    }

    private int _mergeCapacity;

    public int MergeCapacity
    {
        get => _mergeCapacity;
        private set => _mergeCapacity = value;
    }

    private ThingDef? _itemStuff;

    public ThingDef ItemStuff
    {
        get
        {
            _itemStuff ??= Items.First().Stuff;

            return _itemStuff;
        }
        private set => _itemStuff = value;
    }

    private int _numMergeStacksWillUse;

    public int NumMergeStacksWillUse
    {
        get => _numMergeStacksWillUse;
        private set => _numMergeStacksWillUse = value;
    }

    public int MovedQuantity;

    private ThingDef? _miniDef;

    public ThingDef? MiniDef
    {
        get
        {
            if (_miniDef == null && (Items.First() as MinifiedThing)?.InnerThing.def != null)
            {
                _miniDef = (Items.First() as MinifiedThing)?.InnerThing.def;
            }

            return _miniDef;
        }
        private set => _miniDef = value;
    }

    private int _playerSetQuantity = -1;

    private int _selectedQuantity;

    public int SelectedQuantity
    {
        get => _selectedQuantity;
        private set => _selectedQuantity = value;
    }

    public int SetQuantity
    {
        get => (_playerSetQuantity == -1) ? SelectedQuantity : _playerSetQuantity;
        set
        {
            if (value < 0 || value > SelectedQuantity) throw new ArgumentOutOfRangeException();
            _playerSetQuantity = value;
        }
    }

    public string Label => GenLabel.ThingLabel(MiniDef ?? ItemDef, ItemStuff, SetQuantity).CapitalizeFirst();


    // 需要占用的物品堆数 🤔不太明白
    public int NumStacksWillUse => StacksWorth(ItemDef, Math.Max(0, SetQuantity - MergeCapacity)) + NumMergeStacksWillUse;

    // 将会存储不同堆不同ID的同种物品
    public List<Thing> Items = [];

    public void ExposeData()
    {
        Scribe_Collections.Look(ref Items, "items", LookMode.Reference);
        Scribe_Values.Look(ref _selectedQuantity, "selectedQuantity");
        Scribe_Values.Look(ref _playerSetQuantity, "setQuantity");
        Scribe_Values.Look(ref _mergeCapacity, "mergeCapacity");
        Scribe_Values.Look(ref _numMergeStacksWillUse, "numMergeStacksWillUse");
        Scribe_Values.Look(ref MovedQuantity, "movedQuantity");
        Scribe_Defs.Look(ref _itemDef, "itemDef");
        Scribe_Defs.Look(ref _itemStuff, "itemStuff"); // 
        Scribe_Defs.Look(ref _miniDef, "miniDef"); // 
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
        bool result = true;
        result = result && t.def.category == ThingCategory.Item;
        result = result && t.def == ItemDef;
        if (t.Stuff != null)
        {
            result = result && t.Stuff == ItemStuff;
        }

        if (MiniDef != null && (t as MinifiedThing)?.InnerThing.def != null)
        {
            result = result && (t as MinifiedThing)?.InnerThing.def == MiniDef;
        }

        return result;
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
        if (!r || !playerCancelled) return r;

        // 如果是 Command_Cancel_HaulExplicitly
        SelectedQuantity -= t.stackCount;
        _playerSetQuantity = Math.Min(_playerSetQuantity, SelectedQuantity);
        return r;
    }

    public int GetNumRemainingToHaul()
    {
        var pawnsList = new List<Pawn>(ParentData.Map.mapPawns.PawnsInFaction(Faction.OfPlayer)); // 🤔不如加个是否能从事搬运工作的判断
        int beingHauledNow = 0;
        foreach (Pawn p in pawnsList)
        {
            if (p.jobs.curJob == null)
            {
                continue;
            }

            if (p.jobs.curJob.def.driverClass == typeof(JobDriver_HaulExplicitly) && this == ((JobDriver_HaulExplicitly)p.jobs.curDriver).record)
            {
                beingHauledNow += p.jobs.curJob.count; // count 是该 job 向目的地搬运的物品数目
            }
        }

        return Math.Max(0, SetQuantity - (MovedQuantity + beingHauledNow));
    }
}