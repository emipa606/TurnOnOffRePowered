using Verse;

namespace TurnOnOffRePowered;

// Mod settings storage (serialized)
public class TurnOnOffSettings : ModSettings
{
    public bool applyRepowerVanilla = true;
    public bool blockUseWhenLowPower = true;
    public float doorMultiplier = 10f;
    public float highMultiplier = 2.5f;
    public float lowValue = 10f;
    public bool verboseLogging = false;

    public override void ExposeData()
    {
        Scribe_Values.Look(ref lowValue, "lowValue", 10f);
        Scribe_Values.Look(ref highMultiplier, "highMultiplier", 2.5f);
        Scribe_Values.Look(ref doorMultiplier, "doorMultiplier", 10f);
        Scribe_Values.Look(ref applyRepowerVanilla, "applyRepowerVanilla", true);
        Scribe_Values.Look(ref blockUseWhenLowPower, "blockUseWhenLowPower", true);
        Scribe_Values.Look(ref verboseLogging, "verboseLogging", false);
        base.ExposeData();
    }

    public void SetToDefaults()
    {
        lowValue = 10f;
        highMultiplier = 2.5f;
        doorMultiplier = 10f;
        applyRepowerVanilla = true;
        blockUseWhenLowPower = true;
        verboseLogging = false;
    }
}
