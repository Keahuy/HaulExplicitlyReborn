using HaulExplicitly.Extension;
using HaulExplicitly.Gizmo;
using UnityEngine;
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

    public static IEnumerable<Verse.Gizmo> GetHaulExplicitlyGizmos(Thing t)
    {
        if (t.def.EverHaulable)
        {
            yield return new Designator_HaulExplicitly();
        }
    }
    
    public static void UpdataLabelAndIcon(this Command_AutoForbiddenAfterHaulExplicitly instance ,Thing t)
    {
        var data = GameComponent_HaulExplicitly.GetManager(t).DataWithItem(t);
        if (data!= null)
        {
            if (data.itemsWillForbidden.Contains(t))
            {
                instance.defaultLabel = "HaulExplicitly.AutoForbiddenAfterHaulExplicitlyIsOnLabel".Translate();
                instance.icon = ContentFinder<Texture2D>.Get("Buttons/AutoForbiddenAfterHaulExplicitlyForbid");
            }
            else
            {
                instance.defaultLabel = "HaulExplicitly.AutoForbiddenAfterHaulExplicitlyIsOffLabel".Translate();
                instance.icon = ContentFinder<Texture2D>.Get("Buttons/AutoForbiddenAfterHaulExplicitlyNoForbid");
            }
        }
    }
}