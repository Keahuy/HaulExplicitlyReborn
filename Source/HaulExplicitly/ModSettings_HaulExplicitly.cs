using Verse;

namespace HaulExplicitly;

public class ModSettings_HaulExplicitly : ModSettings
{
    public List<string> shownVersions = new();

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref shownVersions, "HaulExplicitly_shownVersions", LookMode.Value);
        if (shownVersions == null)
        {
            shownVersions = new List<string>();
        }
    }
}