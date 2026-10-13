using HaulExplicitly.AI;
using HaulExplicitly.Gizmo;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulExplicitly;

/// <summary>
/// 本模组的通用工具方法集合：格子物品查询、Gizmo 生成、按钮状态刷新以及工作中断。
/// </summary>
public static class Utilities
{
    /// <summary>
    /// 查找指定格子上第一个被定义为“始终可搬运”（<c>ThingDef.alwaysHaulable</c>）的物品。
    /// </summary>
    /// <param name="c">要检索的地图格子。</param>
    /// <param name="map">格子所在的地图。</param>
    /// <returns>该格子上第一个 <c>alwaysHaulable</c> 的 <see cref="Thing"/>；若没有则返回 <c>null</c>。</returns>
    public static Thing? GetFirstAlwaysHaulable(this IntVec3 c, Map map)
    {
        List<Thing> list = map.thingGrid.ThingsListAt(c);
        return Enumerable.FirstOrDefault(list, t => t.def.alwaysHaulable);
    }

    /// <summary>
    /// 生成物品的“显式搬运”设计器按钮（仅当物品可以被搬运时生成）。
    /// </summary>
    /// <param name="t">要为其生成按钮的物品。</param>
    /// <returns>物品可搬运时产出一个 <see cref="Designator_HaulExplicitly"/>；否则不产出任何元素。</returns>
    public static IEnumerable<Verse.Gizmo> GetHaulExplicitlyGizmos(Thing t)
    {
        if (t.def.EverHaulable)
        {
            yield return new Designator_HaulExplicitly();
        }
    }
    
    /// <summary>
    /// 根据物品当前“搬运后自动禁用”的状态，刷新命令按钮的标签与图标。
    /// </summary>
    /// <param name="instance">要刷新的命令按钮。</param>
    /// <param name="t">按钮所对应的物品；若该物品不在任何显式搬运数据中则不修改按钮。</param>
    public static void UpdateLabelAndIcon(this Command_AutoForbiddenAfterHaulExplicitly instance ,Thing t)
    {
        var data = GameComponent_HaulExplicitly.GetManager(t).DataWithItem(t);
        if (data == null) return;
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
    
    /// <summary>
    /// 终止所有正在搬运（或排队搬运）指定物品的 <see cref="JobDriver_HaulExplicitly"/> 工作。
    /// </summary>
    /// <param name="t">要停止搬运的目标物品。</param>
    /// <remarks>会同时检查玩家派系每一名殖民者的当前工作与工作队列。</remarks>
    public static void RemoveCurrentHaulExplicitlyJob(Thing t)
    {
        foreach (Pawn p in Find.CurrentMap.mapPawns.PawnsInFaction(Faction.OfPlayer).ListFullCopy())
        {
            var jobs = new List<Job>(p.jobs.jobQueue.AsEnumerable().Select(j => j.job));
            if (p.CurJob != null)
            {
                jobs.Add(p.CurJob);
            }
            foreach (var job in jobs.Where(job => job.def.driverClass == typeof(JobDriver_HaulExplicitly) && job.targetA.Thing == t))
            {
                p.jobs.EndCurrentOrQueuedJob(job, JobCondition.Incompletable);
            }
        }
    }
}