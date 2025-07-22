using HarmonyLib;
using RimWorld;

namespace TurnOnOffRePowered.HarmonyPatches;

// Track the power users
[HarmonyPatch(typeof(Building_WorkTable), nameof(Building_WorkTable.UsedThisTick))]
public static class Building_WorkTable_UsedThisTick
{
    public static void Prefix(Building_WorkTable __instance)
    {
        TurnItOnandOff.AddBuildingUsed(__instance);
    }
}