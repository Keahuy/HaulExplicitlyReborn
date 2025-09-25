using RimWorld;
using Verse;

namespace HaulExplicitly;

public class GameComponent_UpdateNotifier : GameComponent
{
    public GameComponent_UpdateNotifier(Game game) { }

    public override void FinalizeInit()
    {
        base.FinalizeInit();

        string version = HaulExplicitlyMod.CurrentVersion;
        if (!HaulExplicitlyMod.settings!.shownVersions.Contains(version))
        {
            HaulExplicitlyMod.settings.shownVersions.Add(version);
            HaulExplicitlyMod.settings.Write(); // 保存到 config

            LongEventHandler.ExecuteWhenFinished(() =>
            {
                Find.LetterStack.ReceiveLetter("HaulExplicitly.HaulExplicitlyUpdateNoticeLabel".Translate(), "HaulExplicitly.HaulExplicitlyUpdateNoticeLabelDesc".Translate(version), LetterDefOf.NeutralEvent);
            });
        }
    }
}