using Verse;

namespace HaulExplicitly;

public static class Utilities
{
    public static Thing? GetFirstAlwaysHaulable(this IntVec3 c, Map map)
    {
        List<Thing> list = map.thingGrid.ThingsListAt(c);
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].def.alwaysHaulable)
            {
                return list[i];
            }
        }
        return null;
    }
}