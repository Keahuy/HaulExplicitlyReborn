using RimWorld;
using Verse;

namespace HaulExplicitly;

public static class Utilities
{
    public static bool IsHasHaulDesignation(this Thing t)
    {
        // true == t 已被指派 haul 
        return t.MapHeld.designationManager.DesignationOn(t, DesignationDefOf.Haul) != null;
    }

    public static bool IsAlwaysHaulableOrIsSetToHaulable(this Thing t)
    {
        // 如果 t 怎样都没法 haul
        if (!t.def.EverHaulable)
        {
            return false;
        }

        return t.def.alwaysHaulable != t.IsHasHaulDesignation();
    }

    public static bool IsAlwaysHaulableAndIsSetToUnhaulable(this Thing t)
    {
        if (!t.def.EverHaulable)
        {
            return false;
        }
        if (t.def.designateHaulable && !t.IsInValidStorage())
        {
            return false;
        }

        return t.def.alwaysHaulable == t.IsHasHaulDesignation();
    }
    
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