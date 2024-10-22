using HarmonyLib;
using RimWorld;
using Verse;

namespace TurnOnOffRePowered.HarmonyPatches;

[HarmonyPatch(typeof(JobDriver_WatchBuilding), "WatchTickAction")]
public static class JobDriver_WatchBuilding_WatchTickAction
{
    [HarmonyPrefix]
    public static void WatchTickAction(JobDriver_WatchBuilding __instance)
    {
        TurnItOnandOff.AddBuildingUsed(__instance.job.targetA.Thing as Building);
    }
}