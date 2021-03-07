using System;
using HarmonyLib;
using RimWorld;

namespace TurnOnOffRePowered
{
    // Track the power users
    [HarmonyPatch(typeof(Building_WorkTable), "UsedThisTick", new Type[] { })]
    public static class Building_WorkTable_UsedThisTick_Patch
    {
        [HarmonyPrefix]
        public static void UsedThisTick(Building_WorkTable __instance)
        {
            TurnItOnandOff.AddBuildingUsed(__instance);
        }
    }
}