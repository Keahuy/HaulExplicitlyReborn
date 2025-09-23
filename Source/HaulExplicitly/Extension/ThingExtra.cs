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
    private List<Thing>? keys;
    private List<Thing_ExtraData>? values;

    public GameComponent_ThingExtraData(Game game)
    {
    }

    public override void ExposeData()
    {
        // 🤔因为所有需要记录的 thing 的 HaulExplicitly_dontMoved 都为 true 所以根本没必要保存 values 吧
        // 🤔忘保存 HaulExplicitly_isInHaulExplicitlyDest 了
        base.ExposeData();

        if (Scribe.mode == LoadSaveMode.Saving)
        {
            keys = new List<Thing>();
            values = new List<Thing_ExtraData>();
            foreach (var kv in extraData)
            {
                var k = kv.Key;
                var v = kv.Value;
                if (k != null && !k.Destroyed && v != null)
                {
                    keys.Add(k);
                    values.Add(v);
                }
            }
        }

        // 保存/读取两个并行列表（Reference / Deep）
        Scribe_Collections.Look(ref keys, "haulExplicitly_extraData_keys", LookMode.Reference);
        Scribe_Collections.Look(ref values, "haulExplicitly_extraData_values", LookMode.Deep);

        // 读档后重建字典（并跳过 null / 已销毁的 key）
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            extraData.Clear();
            if (keys != null && values != null)
            {
                int count = Math.Min(keys.Count, values.Count);
                for (int i = 0; i < count; i++)
                {
                    var k = keys[i];
                    var v = values[i];
                    if (k != null && v != null && !k.Destroyed)
                    {
                        extraData[k] = v;
                    }
                }
            }

            // 释放临时列表
            keys = null;
            values = null;
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
        if (!thing.def.alwaysHaulable) return;
        DataComp.GetOrCreate(thing).HaulExplicitly_dontMoved = value;
        switch (value)
        {
            case true when thing.MapHeld.designationManager.DesignationOn(thing, HaulExplicitlyDefOf.HaulExplicitly_Unhaul) == null:
                thing.MapHeld.designationManager.AddDesignation(new Designation(thing, HaulExplicitlyDefOf.HaulExplicitly_Unhaul));
                break;
            case false:
                thing.MapHeld.designationManager.TryRemoveDesignationOn(thing, HaulExplicitlyDefOf.HaulExplicitly_Unhaul);
                break;
        }
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