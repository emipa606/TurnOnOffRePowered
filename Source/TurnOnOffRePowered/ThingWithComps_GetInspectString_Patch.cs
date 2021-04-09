using System;
using System.Linq;
using HarmonyLib;
using Verse;

namespace TurnOnOffRePowered
{
    [HarmonyPatch(typeof(ThingWithComps), "GetInspectString", new Type[] { })]
    public static class ThingWithComps_GetInspectString_Patch
    {
        [HarmonyPostfix]
        public static void AddRequiredText(ThingWithComps __instance, ref string __result)
        {
            if (!TurnItOnandOff.buildingsToModifyPowerOn.Contains(__instance)
                || !TurnItOnandOff.powerLevels.ContainsKey(__instance.def.defName))
            {
                return;
            }

            var lowString =
                $"{"PowerNeeded".Translate()}: {TurnItOnandOff.powerLevels[__instance.def.defName][0] * -1} W";
            var lowReplacement =
                $"{"PowerNeeded".Translate()}: {TurnItOnandOff.powerLevels[__instance.def.defName][0] * -1} W ({TurnItOnandOff.powerLevels[__instance.def.defName][1] * -1} W {"powerNeededActive".Translate()})";
            var highString =
                $"{"PowerNeeded".Translate()}: {TurnItOnandOff.powerLevels[__instance.def.defName][1] * -1} W";
            var highReplacement =
                $"{"PowerNeeded".Translate()}: {TurnItOnandOff.powerLevels[__instance.def.defName][1] * -1} W ({TurnItOnandOff.powerLevels[__instance.def.defName][0] * -1} W {"powerNeededInactive".Translate()})";
            if (__result.Contains(lowReplacement) || __result.Contains(highReplacement))
            {
                return;
            }

            __result = __result.Replace(lowString, lowReplacement).Replace(highString, highReplacement);
        }
    }
}