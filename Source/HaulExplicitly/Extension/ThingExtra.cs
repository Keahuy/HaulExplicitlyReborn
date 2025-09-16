using System.Runtime.CompilerServices;
using Verse;

namespace HaulExplicitly.Extension;

class Thing_ExtraData
{
    public bool HaulExplicitly_dontMoved;
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
}