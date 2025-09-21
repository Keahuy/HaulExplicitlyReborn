using System.Runtime.CompilerServices;
using Verse;

namespace HaulExplicitly.Extension;

public class Thing_ExtraData : IExposable
{
    public bool HaulExplicitly_dontMoved; // 如果为 true ，该物品显示 anchor 并且不会被般至储存区。

    public bool HaulExplicitly_isInHaulExplicitlyDest; // 当被 Designator_HaulExplicitly 搬运至目的地后为 true 。如果为 true ，再次 Designator_HaulExplicitly 并 Command_Cancel_HaulExplicitly 时保留 dontMoved = true

    public void ExposeData()
    {
        Scribe_Values.Look(ref HaulExplicitly_dontMoved, "HaulExplicitly_dontMoved", false);
        Scribe_Values.Look(ref HaulExplicitly_isInHaulExplicitlyDest, "HaulExplicitly_isInHaulExplicitlyDest", false);
    }
}

public class GameComponent_ThingExtraData : GameComponent
{
    private Dictionary<Thing, Thing_ExtraData> extraData = new Dictionary<Thing, Thing_ExtraData>();
    private List<Thing> keys;
    private List<Thing_ExtraData> values;

    public GameComponent_ThingExtraData(Game game)
    {
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Collections.Look(ref extraData, "haulExplicitly_extraData", LookMode.Reference, LookMode.Deep, ref keys, ref values);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            // 清理掉可能失效的 key
            extraData.RemoveAll(kv => kv.Key == null || kv.Key.Destroyed);
        }
    }

    public Thing_ExtraData GetOrCreate(Thing thing)
    {
        if (!extraData.TryGetValue(thing, out var data))
        {
            data = new Thing_ExtraData();
            extraData[thing] = data;
        }

        return data;
    }

    public bool TryGet(Thing thing, out Thing_ExtraData data)
    {
        return extraData.TryGetValue(thing, out data);
    }
}

public static class Thing_Extensions
{
    private static GameComponent_ThingExtraData DataComp => Current.Game.GetComponent<GameComponent_ThingExtraData>();

    public static void SetDontMoved(this Thing thing, bool value)
    {
        DataComp.GetOrCreate(thing).HaulExplicitly_dontMoved = value;
    }

    public static bool GetDontMoved(this Thing thing)
    {
        return DataComp.TryGet(thing, out var data) && data.HaulExplicitly_dontMoved;
    }

    public static void SetIsInHaulExplicitlyDest(this Thing thing, bool value)
    {
        DataComp.GetOrCreate(thing).HaulExplicitly_isInHaulExplicitlyDest = value;
    }

    public static bool GetIsInHaulExplicitlyDest(this Thing thing)
    {
        return DataComp.TryGet(thing, out var data) && data.HaulExplicitly_isInHaulExplicitlyDest;
    }
}