using UnityEngine;
using Verse;

namespace HaulExplicitly.Gizmo;

public class Command_AutoForbiddenAfterHaulExplicitly : Command
{
    private Thing thing;

    public Command_AutoForbiddenAfterHaulExplicitly(Thing t)
    {
        thing = t;
        defaultDesc = "HaulExplicitly.AutoForbiddenAfterHaulExplicitlyDesc".Translate();
        hotKey = null;
    }

    public override void ProcessInput(Event ev)
    {
        base.ProcessInput(ev);
        Data_DesignatorHaulExplicitly data = GameComponent_HaulExplicitly.GetManager(Find.CurrentMap).DataWithItem(thing) ?? throw new InvalidOperationException();
        switch (data.SwitchAutoForbidden(thing))
        {
            case -1: break;
            case 0:
                defaultLabel = "HaulExplicitly.AutoForbiddenAfterHaulExplicitlyIsOffLabel".Translate();
                icon = ContentFinder<Texture2D>.Get("Buttons/AutoForbiddenAfterHaulExplicitlyNoForbid");
                break;
            case 1:
                defaultLabel = "HaulExplicitly.AutoForbiddenAfterHaulExplicitlyIsOnLabel".Translate();
                icon = ContentFinder<Texture2D>.Get("Buttons/AutoForbiddenAfterHaulExplicitlyForbid");
                break;
        }
    }

    public static bool RelevantToThing(Thing t)
    {
        return GameComponent_HaulExplicitly.GetManager(t).DataWithItem(t) != null;
    }
}