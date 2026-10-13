using UnityEngine;
using Verse;

namespace HaulExplicitly.Gizmo;

/// <summary>
/// 物品指令按钮：取消该物品的显式搬运任务——把它从搬运记录中移除，并中断正在搬运它的工作。
/// </summary>
public class Command_Cancel_HaulExplicitly : Command
{
    private Thing thing;

    /// <summary>
    /// 为指定物品创建一个「取消显式搬运」按钮。
    /// </summary>
    /// <param name="t">该按钮对应的物品。</param>
    public Command_Cancel_HaulExplicitly(Thing t)
    {
        thing = t;
        defaultLabel = "HaulExplicitly.CancelHaulExplicitlyLabel".Translate();
        icon = ContentFinder<Texture2D>.Get("Buttons/DontHaulExplicitly");
        defaultDesc = "HaulExplicitly.CancelHaulExplicitlyDesc".Translate();
        hotKey = null;
    }

    /// <summary>
    /// 点击时执行取消：把物品从所属搬运记录中移除（按玩家主动取消处理），
    /// 并终止所有正在搬运该物品的工作。
    /// </summary>
    /// <param name="ev">触发本次输入的界面事件。</param>
    public override void ProcessInput(Event ev)
    {
        base.ProcessInput(ev);
        Data_DesignatorHaulExplicitly? data = GameComponent_HaulExplicitly.GetManager(Find.CurrentMap).DataWithItem(thing);
        if (data == null) return;
        data.TryRemoveItem(thing, true);
        Utilities.RemoveCurrentHaulExplicitlyJob(thing);
    }

    /// <summary>
    /// 判断该物品是否需要显示本按钮。
    /// </summary>
    /// <param name="t">目标物品。</param>
    /// <returns>该物品存在于某条搬运记录中时返回 <c>true</c>。</returns>
    public static bool RelevantToThing(Thing t)
    {
        return GameComponent_HaulExplicitly.GetManager(t).DataWithItem(t) != null ;
    }

    
}