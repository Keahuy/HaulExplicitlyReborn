using System.Runtime.CompilerServices;
using Verse;

namespace HaulExplicitly.Extension;

class Thing_ExtraData
{
    public bool HaulExplicitly_dontMoved; // 如果为 true ，该物品显示 anchor 并且不会被般至储存区。
    public bool HaulExplicitly_IsInHaulExplicitlyDest; // 当被 Designator_HaulExplicitly 搬运至目的地后为 true 。如果为 true ，再次 Designator_HaulExplicitly 并 Command_Cancel_HaulExplicitly 时保留 dontMoved = true
}

public static class Thing_Extensions
{
    private static readonly ConditionalWeakTable<Thing, Thing_ExtraData> extraData = new ConditionalWeakTable<Thing, Thing_ExtraData>();

    public static void SetDontMoved(this Thing thing, bool value)
    {
        extraData.GetOrCreateValue(thing).HaulExplicitly_dontMoved = value;
    }

    public static bool GetDontMoved(this Thing thing)
    {
        return extraData.TryGetValue(thing, out var data) && data.HaulExplicitly_dontMoved;
    }

    public static void SetIsInHaulExplicitlyDest(this Thing thing, bool value)
    {
        extraData.GetOrCreateValue(thing).HaulExplicitly_IsInHaulExplicitlyDest = value;
    }

    public static bool GetIsInHaulExplicitlyDest(this Thing thing)
    {
        return extraData.TryGetValue(thing, out var data) && data.HaulExplicitly_IsInHaulExplicitlyDest;
    }
}