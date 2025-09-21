using HaulExplicitly.Extension;
using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace HaulExplicitly;

[UsedImplicitly]
public class GameComponent_HaulExplicitly : GameComponent
{
    private static GameComponent_HaulExplicitly? _instance; // 于构造器中初始化

    private Dictionary<int, JobManager_DesignatorHaulExplicitly> managers = new();

    private List<int> keys;

    private List<JobManager_DesignatorHaulExplicitly> values;
    
    private HashSet<Zone_Stockpile> retainingZones = [];

    public GameComponent_HaulExplicitly(Game game)
    {
        _instance = this;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref managers, "managers", LookMode.Value, LookMode.Deep, ref keys, ref values);
        Scribe_Collections.Look(ref retainingZones, "holdingZones", LookMode.Reference);
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            CleanGarbage();
            GameComponent_HaulExplicitly self = GetInstance();
            var all_zones = Find.Maps.SelectMany(map => map.zoneManager.AllZones).OfType<Zone_Stockpile>().Select(zone => zone).ToList();
            self.retainingZones.IntersectWith(all_zones);
        }
    }

    private static GameComponent_HaulExplicitly GetInstance()
    {
        return _instance ?? throw new NullReferenceException("HaulExplicitly: GameComponent_HaulExplicitly is not instantiated yet.");
    }

    public static List<JobManager_DesignatorHaulExplicitly> GetManagers()
    {
        var self = GetInstance();
        return self.managers.Values.ToList();
    }

    public static JobManager_DesignatorHaulExplicitly GetManager(Thing t)
    {
        if (t.Map != null)
        {
            return GetManager(t.Map);
        }

        return (t.holdingOwner.Owner as Pawn)?.Map != null ? GetManager((t.holdingOwner.Owner as Pawn)?.Map!) : new JobManager_DesignatorHaulExplicitly();
    }

    public static JobManager_DesignatorHaulExplicitly GetManager(Map map)
    {
        var self = GetInstance();
        var r = self.managers.TryGetValue(map.uniqueID);
        if (r != null)
        {
            return r;
        }

        var mgr = new JobManager_DesignatorHaulExplicitly(map);
        self.managers[map.uniqueID] = mgr;
        return mgr;
    }

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
            // 如果该物品不存在，删除该物品的记录
            mgr.CleanGarbage();
        }
    }

    internal static int GetNewHaulExplicitlyDataID()
    {
        GameComponent_HaulExplicitly self = GetInstance();
        var max = self.managers.Values.Aggregate(-1, (current, mgr) => mgr.datas.Values.Select(posting => posting.ID).Prepend(current).Max());
        return max + 1;
    }

    public static void RegisterData(Data_DesignatorHaulExplicitly data)
    {
        JobManager_DesignatorHaulExplicitly manager = GetManager(data.Map);
        foreach (Thing i in data.items)
        {
            if (i is ThingWithComps twc && twc.GetComp<CompForbiddable>() != null)
            {
                i.SetForbidden(false);
            }

            if (!i.GetDontMoved())
            {
                i.SetDontMoved(true);
                if (i.MapHeld.designationManager.DesignationOn(i, HaulExplicitlyDefOf.HaulExplicitly_Unhaul) == null)
                {
                    i.MapHeld.designationManager.AddDesignation(new Designation(i, HaulExplicitlyDefOf.HaulExplicitly_Unhaul));
                }
            }

            foreach (var p2 in manager.datas.Values)
            {
                p2.TryRemoveItem(i);
            }
        }

        if (manager.datas.Keys.Contains(data.ID)) throw new ArgumentException("Data ID " + data.ID + " already exists in this manager.");
        manager.datas[data.ID] = data;
    }
}