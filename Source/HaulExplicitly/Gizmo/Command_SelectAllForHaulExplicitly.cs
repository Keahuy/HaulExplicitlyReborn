using RimWorld;
using UnityEngine;
using Verse;

namespace HaulExplicitly.Gizmo;

/// <summary>
/// 物品指令按钮：一次选中同一条搬运数据中的全部物品，便于统一查看与操作。
/// </summary>
public class Command_SelectAllForHaulExplicitly : Command
{
    /// <summary>
    /// 初始化按钮：标签、图标与说明。
    /// </summary>
    public Command_SelectAllForHaulExplicitly()
    {
        defaultLabel = "HaulExplicitly.SelectAllHaulExplicitlyLabel".Translate();
        icon = ContentFinder<Texture2D>.Get("Buttons/SelectHaulExplicitlyJob");
        defaultDesc = "HaulExplicitly.SelectAllHaulExplicitlyDesc".Translate();
        hotKey = null;
    }

    /// <summary>
    /// 点击时把同一条搬运数据中所有已生成、且尚未被选中的物品加入当前选择。
    /// </summary>
    /// <param name="ev">触发本次输入的界面事件。</param>
    public override void ProcessInput(Event ev)
    {
        base.ProcessInput(ev);
        Selector selector = Find.Selector;
        List<object> selection = selector.SelectedObjects;
        Thing example = (Thing)selection.First();
        Data_DesignatorHaulExplicitly? data = GameComponent_HaulExplicitly.GetManager(example).DataWithItem(example);
        if (data == null) return;
        foreach (Thing t in data.Items)
        {
            if (t != null && !selection.Contains(t) && t.SpawnedOrAnyParentSpawned)
            {
                selector.Select(t);
            }
        }
    }

    /// <summary>
    /// 判断该物品是否需要显示本按钮。
    /// </summary>
    /// <param name="t">目标物品。</param>
    /// <returns>
    /// 该物品属于某条搬运数据、当前选中的对象全都属于同一条数据，
    /// 且该数据中还有已生成但未被选中的物品时返回 <c>true</c>。
    /// </returns>
    public static bool RelevantToThing(Thing t)
    {
        var mgr = GameComponent_HaulExplicitly.GetManager(t);
        Data_DesignatorHaulExplicitly? data = mgr.DataWithItem(t);
        if (data == null) return false;
        foreach (object o in Find.Selector.SelectedObjects)
        {
            if (o is not Thing other || !data.Items.Contains(other)) return false;
        }

        return Find.Selector.SelectedObjects.Count < Enumerable.Count(data.Items, i => i.SpawnedOrAnyParentSpawned);
    }
}