using HarmonyLib;
using Verse;

namespace HaulExplicitly.Patch;

/// <summary>
/// Harmony 补丁入口：在游戏启动时（静态构造器）为整个程序集一次性应用补丁。
/// </summary>
[StaticConstructorOnStartup]
public class HarmonyInit
{
    /// <summary>
    /// 创建 Harmony 实例，并扫描本程序集内的所有补丁类统一应用（<c>PatchAll</c>）。
    /// </summary>
    static HarmonyInit()
    {
        var harmony = new Harmony("likeafox.rimworld.haulexplicitly");
        harmony.PatchAll();
    }
}