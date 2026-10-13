using HaulExplicitly.Extension;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HaulExplicitly.Gizmo;

/// <summary>
/// 「禁止搬运」设计器：把物品标记为「不要自动搬走」，使其显示锚点、不再被自动搬进储存区，
/// 并中断所有正以该物品为目标的工作。
/// </summary>
public class Designator_Unhaul : Designator
{
    /// <summary>
    /// 本设计器对应的标记定义（Unhaul 标记，同时用于绘制锚点）。
    /// </summary>
    protected override DesignationDef Designation => HaulExplicitlyDefOf.HaulExplicitly_Unhaul;

    /// <summary>
    /// 初始化设计器按钮：标签、图标、说明、拖动与成功音效等。
    /// </summary>
    public Designator_Unhaul()
    {
        defaultLabel = "HaulExplicitly.SetUnhaulableLabel".Translate();
        icon = ContentFinder<Texture2D>.Get("Buttons/Unhaulable");
        defaultDesc = "HaulExplicitly.SetUnhaulableDesc".Translate();
        soundDragSustain = SoundDefOf.Designate_DragStandard;
        soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
        useMouseIcon = true;
        soundSucceeded = SoundDefOf.Designate_Haul;
        hotKey = null;
    }

    /// <summary>
    /// 判断格子能否被标记：格子必须在边界内、不在迷雾中，且其上存在「始终可搬运」的物品，
    /// 该物品还需满足 <see cref="CanDesignateThing"/>。
    /// </summary>
    /// <param name="c">被检查的格子。</param>
    /// <returns>表示是否允许的 <see cref="AcceptanceReport"/>。</returns>
    public override AcceptanceReport CanDesignateCell(IntVec3 c)
    {
        if (!c.InBounds(Map) || c.Fogged(Map))
        {
            return false;
        }

        Thing? firstAlwaysHaulable = c.GetFirstAlwaysHaulable(Map);
        if (firstAlwaysHaulable == null)
        {
            return false;
        }

        AcceptanceReport result = CanDesignateThing(firstAlwaysHaulable);
        if (!result.Accepted)
        {
            return result;
        }

        return true;
    }

    /// <summary>
    /// 对格子上第一个「始终可搬运」的物品执行标记。
    /// </summary>
    /// <param name="c">被点击的格子。</param>
    public override void DesignateSingleCell(IntVec3 c)
    {
        DesignateThing(c.GetFirstAlwaysHaulable(Map));
    }

    /// <summary>
    /// 判断物品能否被标记。
    /// </summary>
    /// <param name="t">目标物品。</param>
    /// <returns>只有「始终可搬运」且当前尚未被标记的物品才需要处理。</returns>
    public override AcceptanceReport CanDesignateThing(Thing t)
    {
        return t.def.alwaysHaulable && !t.GetDontMoved();
    }

    /// <summary>
    /// 把物品标记为「不要自动搬走」，并中断当前地图上所有以该物品为目标的工作。
    /// </summary>
    /// <param name="t">目标物品；为 <c>null</c> 时记录错误日志并跳过。</param>
    public override void DesignateThing(Thing? t)
    {
        if (t == null)
        {
            Log.Error("HaulExplicitly: The target things is null when use Unhaul command.");
            return;
        }

        t.SetDontMoved(true);// 标记为禁止搬运
        
        // 取消与该物品有关的工作
        foreach (Pawn p in Find.CurrentMap.mapPawns.PawnsInFaction(Faction.OfPlayer).ListFullCopy())
        {
            var jobs = new List<Job>(p.jobs.jobQueue.AsEnumerable().Select(j => j.job));
            if (p.CurJob != null)
            {
                jobs.Add(p.CurJob);
            }

            foreach (var job in jobs)
            {
                if (job.targetA.Thing == t)
                {
                    p.jobs.EndCurrentOrQueuedJob(job, JobCondition.Incompletable);
                }
            }
        }
    }
}