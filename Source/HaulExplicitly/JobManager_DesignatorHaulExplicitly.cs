using Verse;

namespace HaulExplicitly;

public class JobManager_DesignatorHaulExplicitly : IExposable
{
    public Dictionary<int, Data_DesignatorHaulExplicitly> datas;

    private Map _map;

    public Map Map { get; private set; }
    
    public IEnumerable<Thing> HaulableThings
    {
        get { return datas.Values.SelectMany(posting => posting.Items); }
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
            datas[k].Clean();
            //var status = postings[k].Status();
            //if (status == HaulExplicitlyJobStatus.Complete
            //    || status == HaulExplicitlyJobStatus.Incompletable)
            //    postings.Remove(k);
        }
    }

    public JobManager_DesignatorHaulExplicitly()
    {
        datas = new Dictionary<int, Data_DesignatorHaulExplicitly>();
    }

    public JobManager_DesignatorHaulExplicitly(Map map)
    {
        this.Map = map;
        datas = new Dictionary<int, Data_DesignatorHaulExplicitly>();
    }

    public Data_DesignatorHaulExplicitly? PostingWithItem(Thing item)
    {
        return datas.Values.FirstOrDefault(posting => posting.Items.Contains(item));
    }
}