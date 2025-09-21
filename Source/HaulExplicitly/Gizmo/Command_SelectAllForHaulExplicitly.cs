using RimWorld;
using UnityEngine;
using Verse;

namespace HaulExplicitly.Gizmo;

public class Command_SelectAllForHaulExplicitly : Command
{
    public Command_SelectAllForHaulExplicitly()
    {
        defaultLabel = "HaulExplicitly.SelectAllHaulExplicitlyLabel".Translate();
        icon = ContentFinder<Texture2D>.Get("Buttons/SelectHaulExplicitlyJob");
        defaultDesc = "HaulExplicitly.SelectAllHaulExplicitlyDesc".Translate();
        hotKey = null;
    }

    public override void ProcessInput(Event ev)
    {
        base.ProcessInput(ev);
        Selector selector = Find.Selector;
        List<object> selection = selector.SelectedObjects;
        Thing example = (Thing)selection.First();
        Data_DesignatorHaulExplicitly? data = GameComponent_HaulExplicitly.GetManager(example).DataWithItem(example);
        if (data == null) return;
        foreach (Thing t in data.items)
        {
            if (t != null && !selection.Contains(t) && t.SpawnedOrAnyParentSpawned)
            {
                selector.Select(t);
            }
        }
    }

    public static bool RelevantToThing(Thing t)
    {
        var mgr = GameComponent_HaulExplicitly.GetManager(t);
        Data_DesignatorHaulExplicitly? data = mgr.DataWithItem(t);
        if (data == null) return false;
        foreach (object o in Find.Selector.SelectedObjects)
        {
            if (o is not Thing other || !data.items.Contains(other)) return false;
        }

        return Find.Selector.SelectedObjects.Count < Enumerable.Count(data.items, i => i.SpawnedOrAnyParentSpawned);
    }
}