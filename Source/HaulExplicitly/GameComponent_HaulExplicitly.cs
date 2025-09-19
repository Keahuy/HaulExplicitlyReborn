using HaulExplicitly.Extension;
using JetBrains.Annotations;
using RimWorld;
using Verse;

namespace HaulExplicitly;

[UsedImplicitly]
public class GameComponent_HaulExplicitly : GameComponent
{
    private static GameComponent_HaulExplicitly? _instance;// 于构造器中初始化

    private Dictionary<int, JobManager_DesignatorHaulExplicitly> managers = new();

    public GameComponent_HaulExplicitly(Game game)
    {
        _instance = this;
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
            return r;
        var mgr = new JobManager_DesignatorHaulExplicitly(map);
        self.managers[map.uniqueID] = mgr;
        return mgr;
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
        foreach (Thing i in data.Items)
        {
            {
                ThingWithComps? twc = i as ThingWithComps;
                if (twc != null && twc.GetComp<CompForbiddable>() != null)
                {
                    i.SetForbidden(false);
                }
            }
            if (!i.GetDontMoved())
            {
                i.SetDontMoved(true);
            }

            foreach (var p2 in manager.datas.Values)
            {
                p2.TryRemoveItem(i);
            }
        }

        if (manager.datas.Keys.Contains(data.ID))
            throw new ArgumentException("Posting ID " + data.ID + " already exists in this manager.");
        manager.datas[data.ID] = data;
    }
}