using HarmonyLib;
using RimWorld;

namespace TurnOnOffRePowered;

// Track the power users
[HarmonyPatch(typeof(Building_WorkTable), nameof(Building_WorkTable.UsedThisTick))]
public static class Building_WorkTable_UsedThisTick_Patch
{
    [HarmonyPrefix]
    public static void UsedThisTick(Building_WorkTable __instance)
    {
        TurnItOnandOff.AddBuildingUsed(__instance);
    }
}