using HaulExplicitly.Extension;
using RimWorld;
using UnityEngine;
using Verse;

namespace HaulExplicitly.Gizmo;

/// <summary>
/// 「恢复可搬运」设计器：把被标记为「不要自动搬走」的物品恢复为可被自动搬运的状态，
/// 同时清除锚点与「已到达显式搬运目的地」标记。
/// </summary>
public class Designator_Rehaul : Designator
{
    /// <summary>
    /// 初始化设计器按钮：标签、图标、说明、拖动与成功音效等。
    /// </summary>
    public Designator_Rehaul()
    {
        defaultLabel = "HaulExplicitly.SetHaulableLabel".Translate();
        icon = ContentFinder<Texture2D>.Get("Buttons/Haulable");
        defaultDesc = "HaulExplicitly.SetHaulableDesc".Translate();
        soundDragSustain = SoundDefOf.Designate_DragStandard;
        soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
        useMouseIcon = true;
        soundSucceeded = SoundDefOf.Designate_Haul;
        hotKey = null;
    }

    /// <summary>
    /// 判断格子能否被指定：格子必须在边界内、不在迷雾中，且其上存在被定义为「始终可搬运」的物品，
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
    /// 对格子上第一个「始终可搬运」的物品执行本操作。
    /// </summary>
    /// <param name="c">被点击的格子。</param>
    public override void DesignateSingleCell(IntVec3 c)
    {
        DesignateThing(c.GetFirstAlwaysHaulable(Map));
    }

    /// <summary>
    /// 判断物品能否执行本操作。
    /// </summary>
    /// <param name="t">目标物品。</param>
    /// <returns>只有当前被标记为「不要自动搬走」的物品才需要恢复，返回其 <see cref="AcceptanceReport"/>。</returns>
    public override AcceptanceReport CanDesignateThing(Thing t)
    {
        return t.GetDontMoved();
    }

    /// <summary>
    /// 恢复物品的可搬运状态：清除「不要自动搬走」与「已到达显式搬运目的地」两个标记。
    /// </summary>
    /// <param name="t">目标物品；为 <c>null</c> 时记录错误日志并跳过。</param>
    public override void DesignateThing(Thing? t)
    {
        if (t == null)
        {
            Log.Error("HaulExplicitly: The target things is null when use Rehaul command.");
        }

        t?.SetDontMoved(false);
        t?.SetIsInHaulExplicitlyDest(false); // 
        /*Map.designationManager.TryRemoveDesignationOn(t, HaulExplicitlyDefOf.HaulExplicitly_Unhaul);*/
    }
}