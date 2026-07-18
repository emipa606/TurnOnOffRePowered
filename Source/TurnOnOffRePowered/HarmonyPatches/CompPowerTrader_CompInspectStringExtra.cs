using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using RimWorld;
using Verse;

namespace TurnOnOffRePowered.HarmonyPatches;

[HarmonyPatch(typeof(CompPowerTrader), nameof(CompPowerTrader.CompInspectStringExtra))]
public static class CompPowerTrader_CompInspectStringExtra
{
    public static void Postfix(CompPowerTrader __instance, ref string __result)
    {
        var parent = __instance.parent;
        if (!TurnItOnUtility.buildingsToModifyPowerOn.Contains(parent)
            || !TurnItOnUtility.powerLevels.TryGetValue(parent.def.defName, out var powerLevels))
        {
            return;
        }

        // Overrides: Apply override if present
        if (TurnItOnUtility.TryGetOverridedPowerLevels(parent.def.defName, out var overrideValue))
        {
            powerLevels = overrideValue;
        }

        var newString = TurnItOnUtility.buildingsThatWereUsedLastTick.Contains(parent)
            ? $"{"PowerNeeded".Translate()}: {powerLevels[1] * -1} {"unitOfPower".Translate()} ({powerLevels[0] * -1} {"unitOfPower".Translate()} {"powerNeededInactive".Translate()})\n"
            : $"{"PowerNeeded".Translate()}: {powerLevels[0] * -1} {"unitOfPower".Translate()} ({powerLevels[1] * -1} {"unitOfPower".Translate()} {"powerNeededActive".Translate()})\n";
        var pattern = $"{"PowerNeeded".Translate()}.*\\n";
        __result = Regex.Replace(__result, pattern, newString);
    }
}