using RimWorld;
using Verse;

namespace HaulExplicitly;

/// <summary>
/// 本模组用到的 DefOf 集合：集中持有 XML 中定义的 Def，便于在代码里按字段名访问。
/// </summary>
[DefOf]
public class HaulExplicitlyDefOf
{
    /// <summary>
    /// 「禁止搬运」标记定义（<c>Defs/DesignationDefs/Designations.xml</c> 中的 <c>HaulExplicitly_Unhaul</c>），
    /// 用于在地图上标记「不要自动搬走」的物品并绘制锚点。
    /// </summary>
    public static readonly DesignationDef HaulExplicitly_Unhaul = null!;
}