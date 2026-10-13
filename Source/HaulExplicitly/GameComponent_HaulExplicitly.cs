using HaulExplicitly.Extension;
using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace HaulExplicitly;

/// <summary>
/// 本模组在存档中注册的游戏组件，是所有显式搬运数据的访问入口：
/// 按地图维护一个 <see cref="JobManager_DesignatorHaulExplicitly"/>，并负责存读档时的数据清理。
/// </summary>
[UsedImplicitly]
public class GameComponent_HaulExplicitly : GameComponent
{
    /// <summary>
    /// 当前游戏中的组件实例，在构造器中被赋值。
    /// </summary>
    private static GameComponent_HaulExplicitly? _instance; // 于构造器中初始化

    /// <summary>
    /// 以地图 <c>uniqueID</c> 为键、该地图的搬运管理器为值的字典。
    /// </summary>
    private Dictionary<int, JobManager_DesignatorHaulExplicitly> managers = new();

    /// <summary>
    /// 由 RimWorld 在创建游戏时调用的构造器，记录组件单例。
    /// </summary>
    /// <param name="game">当前游戏存档对象。</param>
    public GameComponent_HaulExplicitly(Game game)
    {
        _instance = this;
    }

    /// <summary>
    /// 存读档钩子：保存前先清理垃圾数据，然后读写全部地图的搬运管理器。
    /// </summary>
    public override void ExposeData()
    {
        base.ExposeData();
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            CleanGarbage();
        }

        Scribe_Collections.Look(ref managers, "managers", LookMode.Value, LookMode.Deep);
    }

    /// <summary>
    /// 获取组件单例。
    /// </summary>
    /// <returns>当前游戏的 <see cref="GameComponent_HaulExplicitly"/> 实例。</returns>
    /// <exception cref="NullReferenceException">当组件尚未被实例化时抛出。</exception>
    private static GameComponent_HaulExplicitly GetInstance()
    {
        return _instance ?? throw new NullReferenceException("HaulExplicitly: GameComponent_HaulExplicitly is not instantiated yet.");
    }

    /// <summary>
    /// 获取所有已建立的地图搬运管理器。
    /// </summary>
    /// <returns>管理器列表（新建的临时列表，修改它不会影响内部字典）。</returns>
    public static List<JobManager_DesignatorHaulExplicitly> GetManagers()
    {
        var self = GetInstance();
        return self.managers.Values.ToList();
    }

    /// <summary>
    /// 获取物品所属地图的搬运管理器。
    /// </summary>
    /// <param name="t">要查询的物品。</param>
    /// <returns>
    /// 物品所在地图的管理器；若物品不在地上（例如被殖民者携带），则尝试使用其持有者所在地图的管理器；
    /// 两者都不可用时返回一个未绑定地图的空管理器。
    /// </returns>
    public static JobManager_DesignatorHaulExplicitly GetManager(Thing t)
    {
        if (t.Map != null)
        {
            return GetManager(t.Map);
        }

        return (t.holdingOwner.Owner as Pawn)?.Map != null
            ? GetManager((t.holdingOwner.Owner as Pawn)?.Map!)
            : new JobManager_DesignatorHaulExplicitly();
    }

    /// <summary>
    /// 获取指定地图的搬运管理器；若尚不存在则创建并登记。
    /// </summary>
    /// <param name="map">目标地图。</param>
    /// <returns>该地图的管理器，保证非空。</returns>
    public static JobManager_DesignatorHaulExplicitly GetManager(Map map)
    {
        var self = GetInstance();
        JobManager_DesignatorHaulExplicitly? r = self.managers!.TryGetValue(map.uniqueID);
        if (r != null)
        {
            return r;
        }

        var mgr = new JobManager_DesignatorHaulExplicitly(map);
        self.managers[map.uniqueID] = mgr;
        return mgr;
    }

    /// <summary>
    /// 清理垃圾数据：删除已不存在的地图所对应的管理器，并让每个存活的管理器清理其内部失效数据。
    /// </summary>
    public static void CleanGarbage()
    {
        GameComponent_HaulExplicitly self = GetInstance();
        // 找出所有已经不活跃的地图对应的 key
        var keys = new HashSet<int>(self.managers.Keys);
        keys.ExceptWith(Find.Maps.Select(m => m.uniqueID));
        foreach (int k in keys)
        {
            // 把这些 key 以及它们对应的 JobManager_DesignatorHaulExplicitly 删掉
            self.managers.Remove(k);
        }

        foreach (JobManager_DesignatorHaulExplicitly mgr in self.managers.Values)
        {
            mgr.CleanGarbage();
        }

    }

    /// <summary>
    /// 生成一个新的显式搬运数据 ID（取所有地图现有数据的最大 ID 加一）。
    /// </summary>
    /// <returns>全局唯一的数据 ID。</returns>
    internal static int GetNewHaulExplicitlyDataID()
    {
        GameComponent_HaulExplicitly self = GetInstance();
        var max = self.managers.Values.Aggregate(-1, (current, mgr) => mgr.datas.Values.Select(data => data.ID).Prepend(current).Max());
        return max + 1;
    }

    /// <summary>
    /// 登记一批新的显式搬运数据，并处理其包含的物品：把它们从旧的搬运数据中移除、
    /// 中断正在进行的相关搬运工作、解除禁用，并标记为「不要自动搬走」。
    /// </summary>
    /// <param name="data">要登记的搬运数据，其 ID 与地图会被用作归档依据。</param>
    /// <exception cref="ArgumentException">当同一张地图上已存在相同 ID 的数据时抛出。</exception>
    public static void RegisterData(Data_DesignatorHaulExplicitly data)
    {
        JobManager_DesignatorHaulExplicitly manager = GetManager(data.Map);
        foreach (Thing i in data.Items)
        {
            foreach (var d in manager.datas.Values)
            {
                d.TryRemoveItem(i);
            }

            Utilities.RemoveCurrentHaulExplicitlyJob(i);

            if (i is ThingWithComps twc && twc.GetComp<CompForbiddable>() != null)
            {
                i.SetForbidden(false);
            }

            if (!i.GetDontMoved())
            {
                i.SetDontMoved(true);
            }
        }

        if (manager.datas.Keys.Contains(data.ID)) throw new ArgumentException("Data ID " + data.ID + " already exists in this manager.");
        manager.datas[data.ID] = data;
    }
}