using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace HaulExplicitly.Gizmo;

/// <summary>
/// 「显式搬运」设计器：玩家选中物品后，用它在地图上指定搬运目的地并生成
/// <see cref="Data_DesignatorHaulExplicitly"/>；同时负责绘制目的地预览与左下角的待搬运物品列表。
/// </summary>
public class Designator_HaulExplicitly : Designator
{
    private static Data_DesignatorHaulExplicitly? data;

    /// <summary>
    /// 清空当前正在编辑的搬运数据（提交或结束一次设计时调用）。
    /// </summary>
    public static void ResetJob()
    {
        data = null;
    }

    /// <summary>
    /// 依据玩家当前选中的物品，新建一条搬运数据。
    /// </summary>
    public static void UpdateJob()
    {
        // 获取所有选中的物品
        List<object> objects = Find.Selector.SelectedObjects;
        data = new Data_DesignatorHaulExplicitly(objects);
    }
    
    /// <summary>
    /// 初始化设计器按钮：标签、图标、说明、拖动与成功音效等。
    /// </summary>
    public Designator_HaulExplicitly()
    {
        defaultLabel = "HaulExplicitly.HaulExplicitlyLabel".Translate();
        icon = ContentFinder<Texture2D>.Get("Buttons/HaulExplicitly");
        defaultDesc = "HaulExplicitly.HaulExplicitlyDesc".Translate();
        soundDragSustain = SoundDefOf.Designate_DragStandard;
        soundDragChanged = SoundDefOf.Designate_DragStandard_Changed;
        useMouseIcon = true;
        soundSucceeded = SoundDefOf.Designate_Haul;
        hotKey = null;
    }
    
    /// <summary>
    /// 设计器被选中时调用：清除上一次遗留的数据，并根据当前选择重建搬运数据。
    /// </summary>
    public override void Selected()
    {
        // 清除上一次使用（可能）遗留的Data
        ResetJob();
        // 新建Data存储
        UpdateJob();
    }
    
    /// <summary>
    /// 判断设计器是否可以继续保持选中状态。
    /// </summary>
    /// <returns>当前存在待编辑的搬运数据时返回 <c>true</c>。</returns>
    public override bool CanRemainSelected()
    {
        return data != null;
    }
    
    /// <summary>
    /// 每帧刷新时调用：依据鼠标位置重新计算目的地，并把可用的目的地格子绘制为高亮平面。
    /// </summary>
    public override void SelectedUpdate()
    {
        if (data == null) return;
        if (!data.TryMakeDestinations(UI.MouseMapPosition()) || data.destinations == null) return;
        float alt = AltitudeLayer.MetaOverlays.AltitudeFor();
        foreach (var drawPos in data.destinations.Select(d => d.ToVector3ShiftedWithAltitude(alt)))
        {
            Graphics.DrawMesh(MeshPool.plane10, drawPos, Quaternion.identity, DesignatorUtility.DragHighlightThingMat, 0);
        }
    }
    
    /// <summary>
    /// 判断某个格子能否作为设计目标，即当前位置是否能够算出足够的目的地。
    /// </summary>
    /// <param name="loc">被检查的格子。</param>
    /// <returns>表示是否允许在此格设计的 <see cref="AcceptanceReport"/>。</returns>
    public override AcceptanceReport CanDesignateCell(IntVec3 loc)
    {
        return data != null && data.TryMakeDestinations(UI.MouseMapPosition());
    }
    
    /// <summary>
    /// 玩家点击时提交搬运指令：重新计算一次目的地，把数据登记到游戏组件，并结束本次设计。
    /// </summary>
    /// <param name="c">被点击的格子。</param>
    public override void DesignateSingleCell(IntVec3 c)
    {
        Data_DesignatorHaulExplicitly dataLocal = data ?? throw new InvalidOperationException();
        dataLocal.TryMakeDestinations(UI.MouseMapPosition(), false);
        GameComponent_HaulExplicitly.RegisterData(dataLocal);
        ResetJob();
    }
    
    private Vector2 _scrollPosition = Vector2.zero;
    private float _guiLastDrawnHeight;

    // 显示左下角待搬运物品列表
    /// <summary>
    /// 在界面左下角绘制待搬运物品列表：每行显示图标与名称，数量大于 1 时提供按钮打开数量调节滑块。
    /// </summary>
    /// <param name="leftX">绘制区域左边界。</param>
    /// <param name="bottomY">绘制区域下边界。</param>
    public override void DoExtraGuiControls(float leftX, float bottomY)
    {
        Data_DesignatorHaulExplicitly dataLocal = data ?? throw new InvalidOperationException();
        var records = new List<InventoryRecord_DesignatorHaulExplicitly>(dataLocal.inventory.OrderBy(r => r.Label));
        const float max_height = 450f;
        const float width = 268f;
        const float row_height = 28f;
        float height = Math.Min(_guiLastDrawnHeight + 20f, max_height);
        Rect winRect = new Rect(leftX, bottomY - height, width, height);
        Rect outerRect = new Rect(0f, 0f, width, height).ContractedBy(10f);
        Rect innerRect = new Rect(0f, 0f, outerRect.width - 16f, Math.Max(_guiLastDrawnHeight, outerRect.height));
        Find.WindowStack.ImmediateWindow(622372, winRect, WindowLayer.GameUI, delegate
        {
            Widgets.BeginScrollView(outerRect, ref _scrollPosition, innerRect);
            GUI.BeginGroup(innerRect);
            GUI.color = ITab_Pawn_Gear.ThingLabelColor;
            GameFont prev_font = Text.Font;
            Text.Font = GameFont.Small;
            float y = 0f;
            Widgets.ListSeparator(ref y, innerRect.width, "Items to haul");
            foreach (var rec in records)
            {
                Rect rowRect = new Rect(0f, y, innerRect.width - 24f, 28f);
                if (rec.SelectedQuantity > 1)
                {
                    Rect buttonRect = new Rect(rowRect.x + rowRect.width, rowRect.y + (rowRect.height - 24f) / 2, 24f, 24f);
                    if (Widgets.ButtonImage(buttonRect, CaravanThingsTabUtility.AbandonSpecificCountButtonTex))
                    {
                        string txt = "HaulExplicitly.ItemHaulSetQuantity".Translate(new NamedArgument((rec.ItemDef.label).CapitalizeFirst(), "ITEMTYPE"));
                        var dialog = new Dialog_Slider(txt, 1, rec.SelectedQuantity, x => { rec.SetQuantity = x; }, rec.SetQuantity);
                        dialog.layer = WindowLayer.GameUI;
                        Find.WindowStack.Add(dialog);
                    }
                }

                if (Mouse.IsOver(rowRect))
                {
                    GUI.color = ITab_Pawn_Gear.HighlightColor;
                    GUI.DrawTexture(rowRect, TexUI.HighlightTex);
                }

                if (rec.ItemDef.DrawMatSingle?.mainTexture != null)
                {
                    Rect iconRect = new Rect(4f, y, 28f, 28f);
                    if (rec.MiniDef != null || rec.SelectedQuantity == 1)
                        Widgets.ThingIcon(iconRect, rec.items[0]);
                    else
                        Widgets.ThingIcon(iconRect, rec.ItemDef);
                }

                Text.Anchor = TextAnchor.MiddleLeft;
                Text.WordWrap = false;
                Rect textRect = new Rect(36f, y, rowRect.width - 36f, rowRect.height);
                string str = rec.Label;
                Widgets.Label(textRect, str.Truncate(textRect.width));

                y += row_height;
            }

            _guiLastDrawnHeight = y;
            Text.Font = prev_font;
            Text.Anchor = TextAnchor.UpperLeft;
            Text.WordWrap = true;
            GUI.EndGroup();
            Widgets.EndScrollView();
        });
    }
}