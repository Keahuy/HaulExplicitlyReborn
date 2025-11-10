using Verse;

namespace HaulExplicitly;

public class JobManager_DesignatorHaulExplicitly : IExposable
{
    public Dictionary<int, Data_DesignatorHaulExplicitly> datas;

    private Map? _map;

    public Map Map
    {
        get => _map ?? throw new InvalidOperationException();
        private set => _map = value;
    }

    public IEnumerable<Thing> HaulableThings
    {
        get { return datas.Values.SelectMany(data => data.Items); }
    }

    public void ExposeData()
    {
        Scribe_References.Look(ref _map, "map", true);
        Scribe_Collections.Look(ref datas, "datas", LookMode.Value, LookMode.Deep);
    }

    public void CleanGarbage()
    {
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

    public JobManager_DesignatorHaulExplicitly()
    {
        datas = new Dictionary<int, Data_DesignatorHaulExplicitly>();
    }

    public JobManager_DesignatorHaulExplicitly(Map map)
    {
        Map = map;
        datas = new Dictionary<int, Data_DesignatorHaulExplicitly>();
    }

    public Data_DesignatorHaulExplicitly? DataWithItem(Thing item)
    {
        return datas.Values.FirstOrDefault(data => data.Items.Contains(item));
    }
}