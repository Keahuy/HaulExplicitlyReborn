using HaulExplicitly.AI;
using RimWorld;
using Verse;

namespace HaulExplicitly;

/// <summary>
/// 一类物品的搬运记录：保存同种（同 <see cref="ThingDef"/>、同材质、被压缩物品的内部物品也相同）的
/// 全部待搬运物品，以及玩家设定的数量、已搬运数量和「合并到已有物品堆」相关的容量统计。
/// </summary>
public class InventoryRecord_DesignatorHaulExplicitly : IExposable
{
    private WeakReference? _parentData;

    /// <summary>
    /// 本记录所属的搬运数据。内部以弱引用保存，既能在读档后重新定位到正确的
    /// <see cref="Data_DesignatorHaulExplicitly"/> 实例，又避免两者互相强引用而无法被回收。
    /// </summary>
    /// <exception cref="Exception">当遍历所有搬运数据仍找不到所属数据时抛出。</exception>
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

    /// <summary>
    /// 本记录中物品的物品定义，首次访问时从已有物品推断。
    /// </summary>
    public ThingDef ItemDef
    {
        get
        {
            _itemDef ??= items.First().def;

            return _itemDef;
        }
        private set => _itemDef = value;
    }

    private int _mergeCapacity;

    /// <summary>
    /// 合并到目的地已有物品堆时可以额外容纳的数量（这部分物品不需要再占用新的格子）。
    /// </summary>
    public int MergeCapacity
    {
        get => _mergeCapacity;
        private set => _mergeCapacity = value;
    }

    private ThingDef? _itemStuff;

    /// <summary>
    /// 本记录中物品的材质；无材质时为 <c>null</c>。
    /// </summary>
    public ThingDef ItemStuff
    {
        get
        {
            _itemStuff ??= items.First().Stuff;

            return _itemStuff;
        }
        private set => _itemStuff = value;
    }

    private int _numMergeStacksWillUse;

    /// <summary>
    /// 本记录需要为「合并到已有物品堆」额外占用的目的地格子数，每个合并目标记为一格。
    /// </summary>
    public int NumMergeStacksWillUse
    {
        get => _numMergeStacksWillUse;
        private set => _numMergeStacksWillUse = value;
    }

    /// <summary>
    /// 已经搬运到目的地的物品数量。
    /// </summary>
    public int MovedQuantity;

    private ThingDef? _miniDef;

    /// <summary>
    /// 若搬运的是被压缩（minified）的物品，此处记录其内部物品的物品定义，否则为 <c>null</c>。
    /// </summary>
    public ThingDef? MiniDef
    {
        get
        {
            if (items.Any() && _miniDef == null && (items.First() as MinifiedThing)?.InnerThing.def != null)
            {
                _miniDef = (items.First() as MinifiedThing)?.InnerThing.def;
            }

            return _miniDef;
        }
        private set => _miniDef = value;
    }

    private int _playerSetQuantity = -1;

    private int _selectedQuantity;

    /// <summary>
    /// 本记录当前包含的物品总数（随物品的加入与移除同步变化）。
    /// </summary>
    public int SelectedQuantity
    {
        get => _selectedQuantity;
        private set => _selectedQuantity = value;
    }

    /// <summary>
    /// 玩家设定的搬运数量；未手动设定时等同于 <see cref="SelectedQuantity"/>。
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">设定的数量为负数或大于当前 <see cref="SelectedQuantity"/> 时抛出。</exception>
    public int SetQuantity
    {
        get => (_playerSetQuantity == -1) ? SelectedQuantity : _playerSetQuantity;
        set
        {
            if (value < 0 || value > SelectedQuantity) throw new ArgumentOutOfRangeException();
            _playerSetQuantity = value;
        }
    }

    /// <summary>
    /// 用于界面显示的物品名称（包含设定数量）。
    /// </summary>
    public string Label => GenLabel.ThingLabel(MiniDef ?? ItemDef, ItemStuff, SetQuantity).CapitalizeFirst();


    /// <summary>
    /// 完成本次搬运预计需要占用的目的地格子（堆）数：扣除可合并容量后剩余的物品所需堆数，加上合并目标占用的堆数。
    /// </summary>
    public int NumStacksWillUse => StacksWorth(ItemDef, Math.Max(0, SetQuantity - MergeCapacity)) + NumMergeStacksWillUse;

    // 将会存储不同堆不同ID的同种物品
    /// <summary>
    /// 本记录包含的物品，可能来自不同的物品堆、拥有各自不同的 ID。
    /// </summary>
    public List<Thing> items = [];

    /// <summary>
    /// 存读档钩子：保存前先移除已不在地图上的物品，随后读写物品列表、数量、合并统计与物品定义。
    /// </summary>
    public void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving) // 保存存档时
        {
            foreach (var item in items.ToList().Where(item => item.MapHeld == null))
            {
                TryRemoveItem(item); // 有时没有全部搬完地图就被摧毁了，导致需要搬运的物品也被摧毁。加入这个来避免 Exception while saving HaulExplicitly.GameComponent_HaulExplicitly: System.NullReferenceException: Object reference not set to an instance of an object
            }
        }

        Scribe_Collections.Look(ref items, "items", LookMode.Reference);
        Scribe_Values.Look(ref _selectedQuantity, "selectedQuantity");
        Scribe_Values.Look(ref _playerSetQuantity, "setQuantity");
        Scribe_Values.Look(ref _mergeCapacity, "mergeCapacity");
        Scribe_Values.Look(ref _numMergeStacksWillUse, "numMergeStacksWillUse");
        Scribe_Values.Look(ref MovedQuantity, "movedQuantity");
        Scribe_Defs.Look(ref _itemDef, "itemDef");
        Scribe_Defs.Look(ref _itemStuff, "itemStuff");
        Scribe_Defs.Look(ref _miniDef, "miniDef");
    }

    /// <summary>
    /// 供反序列化使用的无参构造器，不初始化任何业务字段。
    /// </summary>
    public InventoryRecord_DesignatorHaulExplicitly()
    {
        // 防报错：SaveableFromNode exception: System.MissingMethodException: Constructor on type 'HaulExplicitly.HaulExplicitlyInventoryRecord' not found.   
    }

    /// <summary>
    /// 为某个物品创建一条新的搬运记录：绑定所属数据（弱引用），记录物品定义、材质、
    /// 被压缩物品的内部物品，并以该物品当前的堆数作为搬运数量。
    /// </summary>
    /// <param name="initial">本记录的第一个物品。</param>
    /// <param name="parentData">该物品所属的搬运数据。</param>
    public InventoryRecord_DesignatorHaulExplicitly(Thing initial, Data_DesignatorHaulExplicitly parentData)
    {
        _parentData = new WeakReference(parentData);
        items.Add(initial);
        ItemDef = initial.def;
        ItemStuff = initial.Stuff;
        MiniDef = (initial as MinifiedThing)?.InnerThing.def;
        SelectedQuantity = initial.stackCount;
        ResetMerge();
    }

    /// <summary>
    /// 重置合并统计：清空可合并容量与需要额外占用的格子数。
    /// </summary>
    public void ResetMerge()
    {
        MergeCapacity = 0;
        NumMergeStacksWillUse = 0;
    }

    /// <summary>
    /// 登记一次「合并到目的地已有物品堆」的操作：额外占用一格，并增加可免费容纳的数量。
    /// </summary>
    /// <param name="itemQuantity">目标格子上已有物品的数量，用于计算该堆剩余可容纳的空间。</param>
    public void AddMergeCell(int itemQuantity)
    {
        NumMergeStacksWillUse++;
        MergeCapacity += ItemDef.stackLimit - itemQuantity;
    }

    /// <summary>
    /// 计算指定数量的物品需要占用多少个「堆」（目的地格子）。
    /// </summary>
    /// <param name="thingDef">物品定义，用于取得单堆上限。</param>
    /// <param name="quantity">物品数量。</param>
    /// <returns>向上取整后的堆数。</returns>
    public static int StacksWorth(ThingDef thingDef, int quantity)
    {
        // 计算物品堆数
        return quantity / thingDef.stackLimit + (quantity % thingDef.stackLimit == 0 ? 0 : 1);
    }

    /// <summary>
    /// 判断指定物品能否并入本记录：必须是同类物品、<see cref="ItemDef"/> 与材质一致，
    /// 若本记录记录的是被压缩物品，其内部物品也必须一致。
    /// </summary>
    /// <param name="t">待判断的物品。</param>
    /// <returns>可以并入返回 <c>true</c>。</returns>
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

    /// <summary>
    /// 判断指定物品是否已包含在本记录中。
    /// </summary>
    /// <param name="t">待判断的物品。</param>
    /// <returns>物品已在本记录中返回 <c>true</c>。</returns>
    public bool HasItem(Thing t)
    {
        bool result = false;
        result = result || items.Contains(t);
        /*if (t.def.saveCompressible)
        {
            result = result || itemCoordinates.Contains(t.Position);
        }*/

        return result;
    }

    /// <summary>
    /// 把一个物品并入本记录。
    /// </summary>
    /// <param name="t">要并入的物品，需通过 <see cref="CanAdd"/> 检查。</param>
    /// <param name="sideEffects">是否同时把该物品的数量计入 <see cref="SelectedQuantity"/>；默认计入，搬运过程中分裂出的新堆则传入 <c>false</c>。</param>
    /// <returns>并入成功返回 <c>true</c>；物品类型不匹配时返回 <c>false</c>。</returns>
    public bool TryAddItem(Thing t, bool sideEffects = true)
    {
        if (!CanAdd(t)) return false;
        items.Add(t);
        if (sideEffects)
        {
            SelectedQuantity += t.stackCount;
        }

        return true;
    }

    /// <summary>
    /// 把一个物品从本记录中移除。
    /// </summary>
    /// <param name="t">要移除的物品；若不在本记录中则不做任何改动。</param>
    /// <param name="playerCancelled">是否为玩家主动取消（为 <c>true</c> 时同步扣减 <see cref="SelectedQuantity"/>，并把玩家设定数量收敛到不超过剩余数量）。</param>
    /// <returns>操作成功返回 <c>true</c>；从物品列表中删除失败时返回 <c>false</c>。</returns>
    public bool TryRemoveItem(Thing t, bool playerCancelled = false)
    {
        bool result = true;
        if (items.Contains(t))
        {
            if (!items.Remove(t))
            {
                return false;
            }
        }

        switch (playerCancelled)
        {
            case false:
                return result;
            case true: // 如果是 Command_Cancel_HaulExplicitly
                SelectedQuantity -= t.stackCount;
                _playerSetQuantity = Math.Min(_playerSetQuantity, SelectedQuantity);
                return result;
        }
    }

    /// <summary>
    /// 计算本记录还需要搬运的数量：设定数量减去已搬运数量以及正被其他殖民者搬运的数量。
    /// </summary>
    /// <returns>剩余需要搬运的数量，最小为 0。</returns>
    public int GetNumRemainingToHaul()
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
                beingHauledNow += p.jobs.curJob.count; // count 是该 job 向目的地搬运的物品数目
            }
        }

        return Math.Max(0, SetQuantity - (MovedQuantity + beingHauledNow));
    }
}