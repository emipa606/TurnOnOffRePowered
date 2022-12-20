using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using RimWorld;
using Verse;

namespace TurnOnOffRePowered;

[HarmonyPatch(typeof(CompPowerTrader), "CompInspectStringExtra")]
public static class CompPowerTrader_CompInspectStringExtra_Patch
{
    [HarmonyPostfix]
    public static void AddRequiredText(CompPowerTrader __instance, ref string __result)
    {
        var parent = __instance.parent;
        if (!TurnItOnandOff.buildingsToModifyPowerOn.Contains(parent)
            || !TurnItOnandOff.powerLevels.ContainsKey(parent.def.defName))
        {
            return;
        }

        var newString = TurnItOnandOff.buildingsThatWereUsedLastTick.Contains(parent)
            ? $"{"PowerNeeded".Translate()}: {TurnItOnandOff.powerLevels[parent.def.defName][1] * -1} {"unitOfPower".Translate()} ({TurnItOnandOff.powerLevels[parent.def.defName][0] * -1} {"unitOfPower".Translate()} {"powerNeededInactive".Translate()})\n"
            : $"{"PowerNeeded".Translate()}: {TurnItOnandOff.powerLevels[parent.def.defName][0] * -1} {"unitOfPower".Translate()} ({TurnItOnandOff.powerLevels[parent.def.defName][1] * -1} {"unitOfPower".Translate()} {"powerNeededActive".Translate()})\n";
        var pattern = $"{"PowerNeeded".Translate()}.*\\n";
        __result = Regex.Replace(__result, pattern, newString);
    }
}