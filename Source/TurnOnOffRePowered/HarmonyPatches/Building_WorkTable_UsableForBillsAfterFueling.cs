using HarmonyLib;
using RimWorld;

namespace TurnOnOffRePowered.HarmonyPatches;

[HarmonyPatch(typeof(Building_WorkTable), nameof(Building_WorkTable.UsableForBillsAfterFueling))]
public static class Building_WorkTable_UsableForBillsAfterFueling
{
    public static void Postfix(Building_WorkTable __instance, ref bool __result)
    {
        if (!__result)
        {
            return;
        }

        if (TurnItOnandOff.HasEnoughPower(__instance))
        {
            return;
        }

        __result = false;
    }
}