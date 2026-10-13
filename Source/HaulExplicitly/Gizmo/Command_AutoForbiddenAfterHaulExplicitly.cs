using UnityEngine;
using Verse;

namespace HaulExplicitly.Gizmo;

/// <summary>
/// 物品指令按钮：切换「搬运后自动禁用」开关。开启后，物品被显式搬运到目的地时会自动设为禁用，
/// 从而不会被再次搬运。
/// </summary>
public class Command_AutoForbiddenAfterHaulExplicitly : Command
{
    private Thing thing;

    /// <summary>
    /// 为指定物品创建一个「搬运后自动禁用」按钮（标签与图标随后由 <c>Utilities.UpdateLabelAndIcon</c> 刷新）。
    /// </summary>
    /// <param name="t">该按钮对应的物品。</param>
    public Command_AutoForbiddenAfterHaulExplicitly(Thing t)
    {
        thing = t;
        defaultDesc = "HaulExplicitly.AutoForbiddenAfterHaulExplicitlyDesc".Translate();
        hotKey = null;
    }

    /// <summary>
    /// 点击时切换该物品的自动禁用状态，并同步更新按钮的标签与图标。
    /// </summary>
    /// <param name="ev">触发本次输入的界面事件。</param>
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

    /// <summary>
    /// 判断该物品是否需要显示本按钮。
    /// </summary>
    /// <param name="t">目标物品。</param>
    /// <returns>该物品存在于某条搬运记录中时返回 <c>true</c>。</returns>
    public static bool RelevantToThing(Thing t)
    {
        return GameComponent_HaulExplicitly.GetManager(t).DataWithItem(t) != null;
    }
}