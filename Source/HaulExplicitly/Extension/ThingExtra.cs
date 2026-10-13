using Verse;

namespace HaulExplicitly.Extension;

/// <summary>
/// 挂在单个物品上的附加状态：是否禁止自动搬走（显示为「锚点」），
/// 以及是否已经由显式搬运放到过目的地。
/// </summary>
public class Thing_ExtraData : IExposable
{
    /// <summary>
    /// 为 true 时该物品会显示「锚点」标记，并且不会被自动搬进储存区。
    /// </summary>
    public bool HaulExplicitly_dontMoved; // 如果为 true ，该物品显示 anchor 并且不会被般至储存区。

    /// <summary>
    /// 物品已被显式搬运到目的地后为 true；此时再次对它使用显式搬运并取消，会保留「不要自动搬走」标记。
    /// </summary>
    public bool HaulExplicitly_isInHaulExplicitlyDest; // 当被 Designator_HaulExplicitly 搬运至目的地后为 true 。如果为 true ，再次 Designator_HaulExplicitly 并 Command_Cancel_HaulExplicitly 时保留 dontMoved = true

    /// <summary>
    /// 存读档钩子：读写「不要自动搬走」与「已到达显式搬运目的地」两个标记。
    /// </summary>
    public void ExposeData()
    {
        Scribe_Values.Look(ref HaulExplicitly_dontMoved, "HaulExplicitly_dontMoved");
        Scribe_Values.Look(ref HaulExplicitly_isInHaulExplicitlyDest, "HaulExplicitly_isInHaulExplicitlyDest");
    }
}

/// <summary>
/// 存档级游戏组件：以「物品 → 附加状态」的字典保存所有显式搬运的物品标记，
/// 并负责存读档时把这些状态与物品重新对应起来。
/// </summary>
public class GameComponent_ThingExtraData : GameComponent
{
    private Dictionary<Thing, Thing_ExtraData> extraData = new();
    private List<Thing>? keys;
    private List<Thing_ExtraData>? values;

    /// <summary>
    /// 由 RimWorld 在创建游戏时调用的构造器，本身不需要初始化数据。
    /// </summary>
    /// <param name="game">当前游戏存档对象。</param>
    public GameComponent_ThingExtraData(Game game)
    {
    }

    /// <summary>
    /// 存读档钩子：保存时把字典拆成两个并行的列表（并跳过已销毁物品），
    /// 读档完成后依据这两个列表重建字典并释放临时列表。
    /// </summary>
    public override void ExposeData()
    {
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

        switch (Scribe.mode)
        {
            // 读档后重建字典（并跳过 null / 已销毁的 key）
            case LoadSaveMode.PostLoadInit:
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
                break;
            }
        }
    }

    /// <summary>
    /// 取得物品的附加状态，不存在时创建并登记。
    /// </summary>
    /// <param name="thing">目标物品。</param>
    /// <returns>该物品对应的附加状态，保证非空。</returns>
    public Thing_ExtraData GetOrCreate(Thing thing)
    {
        if (!extraData.TryGetValue(thing, out var data))
        {
            data = new Thing_ExtraData();
            extraData[thing] = data;
        }

        return data;
    }

    /// <summary>
    /// 尝试取得物品的附加状态。
    /// </summary>
    /// <param name="thing">目标物品。</param>
    /// <param name="data">输出该物品的附加状态。</param>
    /// <returns>该物品存在附加状态时返回 <c>true</c>。</returns>
    public bool TryGet(Thing thing, out Thing_ExtraData data)
    {
        return extraData.TryGetValue(thing, out data);
    }
}

/// <summary>
/// <see cref="Thing"/> 的扩展方法：把物品附加状态包装成「不要自动搬走」的语义，
/// 并同步维护地图上的 Unhaul 标记。
/// </summary>
public static class Thing_Extensions
{
    /// <summary>
    /// 物品附加状态所在的存档游戏组件。
    /// </summary>
    private static GameComponent_ThingExtraData DataComp => Current.Game.GetComponent<GameComponent_ThingExtraData>();

    /// <summary>
    /// 设置物品的「不要自动搬走」标记：为 <c>true</c> 时同时在地图上添加 Unhaul 标记
    /// （用于绘制锚点并阻止自动搬运），为 <c>false</c> 时移除该标记。
    /// </summary>
    /// <param name="thing">目标物品。</param>
    /// <param name="value">是否禁止自动搬走。</param>
    /// <remarks>仅对 <c>def.alwaysHaulable</c> 的物品生效。</remarks>
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

    /// <summary>
    /// 判断物品是否已被标记为「不要自动搬走」。
    /// </summary>
    /// <param name="thing">目标物品。</param>
    /// <returns>已标记返回 <c>true</c>。</returns>
    public static bool GetDontMoved(this Thing thing)
    {
        return DataComp.TryGet(thing, out var data) && data.HaulExplicitly_dontMoved;
    }

    /// <summary>
    /// 记录物品是否已经被显式搬运放到过目的地（影响取消搬运时是否保留「不要自动搬走」标记）。
    /// </summary>
    /// <param name="thing">目标物品。</param>
    /// <param name="value">是否已到达显式搬运目的地。</param>
    public static void SetIsInHaulExplicitlyDest(this Thing thing, bool value)
    {
        DataComp.GetOrCreate(thing).HaulExplicitly_isInHaulExplicitlyDest = value;
    }

    /// <summary>
    /// 判断物品是否已经被显式搬运放到过目的地。
    /// </summary>
    /// <param name="thing">目标物品。</param>
    /// <returns>已到达过显式搬运目的地返回 <c>true</c>。</returns>
    public static bool GetIsInHaulExplicitlyDest(this Thing thing)
    {
        return DataComp.TryGet(thing, out var data) && data.HaulExplicitly_isInHaulExplicitlyDest;
    }
}