using UnityEngine;
using Verse;

namespace HaulExplicitly;

public class HaulExplicitlyMod : Mod
{
    public static ModSettings_HaulExplicitly? settings;


    public static string CurrentVersion => ModLister.GetActiveModWithIdentifier("likeafox.haulexplicitly").ModVersion;


    public HaulExplicitlyMod(ModContentPack content) : base(content)
    {
        settings = GetSettings<ModSettings_HaulExplicitly>(); //读取本地数据
    }

    public override string SettingsCategory()
    {
        return "Haul Explicitly"; // mod选项面板中的名字
    }

    public override void DoSettingsWindowContents(Rect inRect) // 为mod选项面板添加GUI组件
    {
        Widgets.Label(inRect, "Haul Explicitly 没有额外设置。");
    }
}