using HarmonyLib;
using Verse;

namespace HaulExplicitly.PickUpAndHaulCompatibility;

/// <summary>
/// PickUpAndHaul 兼容补丁的入口：在游戏启动时（静态构造器）为整个程序集应用补丁。
/// </summary>
[StaticConstructorOnStartup]
public class PatchMain
{
    /// <summary>
    /// 创建 Harmony 实例并扫描本程序集内的所有补丁类统一应用（<c>PatchAll</c>）。
    /// </summary>
    static PatchMain()
    {
        var harmony = new Harmony("likeafox.rimworld.haulexplicitly");
        harmony.PatchAll();
    }
}