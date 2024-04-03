using HarmonyLib;
using RimWorld;
using Verse;

namespace TurnOnOffRePowered;

[HarmonyPatch(typeof(JobDriver_WatchBuilding), "WatchTickAction")]
public static class JobDriver_WatchBuilding_WatchTickAction_Patch
{
    [HarmonyPrefix]
    public static void WatchTickAction(JobDriver_WatchBuilding __instance)
    {
        TurnItOnandOff.AddBuildingUsed(__instance.job.targetA.Thing as Building);
    }
}