using Verse;

namespace HaulExplicitly;

/// <summary>
/// 某一张地图上的显式搬运数据集合：以数据 ID 为键保存 <see cref="Data_DesignatorHaulExplicitly"/>，
/// 并对外提供「哪些物品正在等待显式搬运」等查询。
/// </summary>
public class JobManager_DesignatorHaulExplicitly : IExposable
{
    /// <summary>
    /// 数据 ID 到搬运数据的映射。
    /// </summary>
    public Dictionary<int, Data_DesignatorHaulExplicitly> datas;

    private Map? _map;

    /// <summary>
    /// 本管理器所属的地图。
    /// </summary>
    /// <exception cref="InvalidOperationException">当管理器尚未绑定地图时抛出。</exception>
    public Map Map
    {
        get => _map ?? throw new InvalidOperationException();
        private set => _map = value;
    }

    /// <summary>
    /// 本地图上全部正在等待显式搬运的物品。
    /// </summary>
    public IEnumerable<Thing> HaulableThings
    {
        get { return datas.Values.SelectMany(data => data.Items); }
    }

    /// <summary>
    /// 存读档钩子：读写所属地图引用和全部搬运数据。
    /// </summary>
    public void ExposeData()
    {
        Scribe_References.Look(ref _map, "map", true);
        Scribe_Collections.Look(ref datas, "datas", LookMode.Value, LookMode.Deep);
    }

    /// <summary>
    /// 清理失效数据：先让已搬完且无人在搬的搬运记录收尾（解除没搬走的剩余物品的「禁止搬运」标记），
    /// 再删除被摧毁物品的记录，并移除「已搬运数量等于设定数量」即已完成的搬运数据。
    /// </summary>
    public void CleanGarbage()
    {
        // 先对已经搬完的记录收尾：把没搬走的剩余物品移出搬运工作并恢复自由搬运
        foreach (Data_DesignatorHaulExplicitly data in datas.Values.ToList())
        {
            foreach (InventoryRecord_DesignatorHaulExplicitly record in data.inventory.ToList())
            {
                if (!record.IsDone) continue;
                record.TryFinishHaulJob();
            }
        }

        var keys = new List<int>(datas.Keys);
        foreach (int k in keys)
        {
            // 获取所有被摧毁了的物品的列表，删除列表中物品的记录
            datas[k].Clean();

            if (!datas[k].inventory.Any(i => i.SelectedQuantity != i.MovedQuantity))
            {
                datas.Remove(k);
            }
            
        }
    }

    /// <summary>
    /// 供反序列化使用的无参构造器：初始化空的搬运数据字典。
    /// </summary>
    public JobManager_DesignatorHaulExplicitly()
    {
        datas = new Dictionary<int, Data_DesignatorHaulExplicitly>();
    }

    /// <summary>
    /// 为指定地图创建搬运管理器。
    /// </summary>
    /// <param name="map">本管理器所属的地图。</param>
    public JobManager_DesignatorHaulExplicitly(Map map)
    {
        Map = map;
        datas = new Dictionary<int, Data_DesignatorHaulExplicitly>();
    }

    /// <summary>
    /// 查找包含指定物品的搬运数据。
    /// </summary>
    /// <param name="item">要查找的物品。</param>
    /// <returns>包含该物品的搬运数据；找不到时返回 <c>null</c>。</returns>
    public Data_DesignatorHaulExplicitly? DataWithItem(Thing item)
    {
        return datas.Values.FirstOrDefault(data => data.Items.Contains(item));
    }
}