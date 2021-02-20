using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using HugsLib;
using HugsLib.Settings;
using RePower;
using RimWorld;
using UnityEngine;
using Verse;

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

    [HarmonyPatch(typeof(JobDriver_WatchBuilding), "WatchTickAction", new Type[] { })]
    public static class JobDriver_WatchBuilding_WatchTickAction_Patch
    {
        [HarmonyPrefix]
        public static void WatchTickAction(JobDriver_WatchBuilding __instance)
        {
            TurnItOnandOff.AddBuildingUsed(__instance.job.targetA.Thing as Building);
        }
    }

    [HarmonyPatch(typeof(ThingWithComps), "GetInspectString", new Type[] { })]
    public static class ThingWithComps_GetInspectString_Patch
    {
        [HarmonyPostfix]
        public static void AddRequiredText(ThingWithComps __instance, ref string __result)
        {
            if (!TurnItOnandOff.buildingsToModifyPowerOn.Contains(__instance))
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

    public class TurnItOnandOff : ModBase
    {
        private static TurnItOnandOff instance;

        // Power levels pairs as Vector2's, X = Idling, Y = In Use
        public static Dictionary<string, Vector2> powerLevels = new Dictionary<string, Vector2>();
        private SettingHandle<bool> applyRepowerVanilla;
        private SettingHandle<float> highMultiplier;

        private int lastVisibleBuildings;
        private SettingHandle<float> lowValue;

        private int ticksToRescan;


        public override string ModIdentifier => "TurnOnOffRePowered";

        public override void Tick(int currentTick)
        {
            if (inUseTick != currentTick)
            {
                inUseTick = currentTick;

                buildingsThatWereUsedLastTick.Clear();
                buildingsThatWereUsedLastTick.UnionWith(buildingsInUseThisTick);
                buildingsInUseThisTick.Clear();
            }

            EvalBeds();
            EvalResearchTables();
            EvalAutodoors();
            EvalDeepDrills();
            EvalHydroponicsBasins();
            EvalScheduledBuildings();

            foreach (var thing in buildingsToModifyPowerOn)
            {
                if (thing == null)
                {
                    Logger.Message("Tried to modify power level for thing which no longer exists");
                    continue;
                }

                var powerComp = thing.TryGetComp<CompPowerTrader>();
                if (powerComp != null)
                {
                    powerComp.PowerOutput = powerLevels[thing.def.defName][0];
                }
            }

            var visibleBuildings = Find.AnyPlayerHomeMap.listerBuildings.allBuildingsColonist.Count;
            if (visibleBuildings != lastVisibleBuildings)
            {
                lastVisibleBuildings = visibleBuildings;
                ticksToRescan = 0;
            }

            --ticksToRescan;
            if (ticksToRescan < 0)
            {
                ticksToRescan = 2000;
                ScanForThings();
            }

            foreach (var building in buildingsThatWereUsedLastTick)
            {
                if (!buildingsToModifyPowerOn.Contains(building))
                {
                    continue;
                }

                var powerComp = building.TryGetComp<CompPowerTrader>();
                if (powerComp != null)
                {
                    powerComp.PowerOutput = powerLevels[building.def.defName][1];
                }
            }
        }

        public static void Log(string log)
        {
            instance?.Logger.Message(log);
        }

        public override void SettingsChanged()
        {
            base.SettingsChanged();
            UpdateDefinitions();
        }

        public override void DefsLoaded()
        {
            lowValue = Settings.GetHandle("lowValue", "lowValue.label".Translate(), "lowValue.tooltip".Translate(), 10f,
                Validators.FloatRangeValidator(1f, 100f));
            highMultiplier = Settings.GetHandle("highMultiplier", "highMultiplier.label".Translate(),
                "highMultiplier.tooltip".Translate(), 2.5f, Validators.FloatRangeValidator(0.1f, 10f));
            applyRepowerVanilla = Settings.GetHandle("applyRepowerVanilla", "applyRepowerVanilla.label".Translate(),
                "applyRepowerVanilla.tooltip".Translate(), true);
            UpdateDefinitions();
        }

        public override void Initialize()
        {
            instance = this;

            Logger.Message("Registered instance");
        }

        private void UpdateRepowerDefs()
        {
            var defs = DefDatabase<RePowerDef>.AllDefs;

            foreach (var def in defs)
            {
                var targetDef = def.targetDef;
                var namedDef = DefDatabase<ThingDef>.GetNamedSilentFail(targetDef);

                if (namedDef == null)
                {
                    continue;
                }

                if (def.poweredWorkbench)
                {
                    RegisterWorkTable(namedDef.defName, def.lowPower, def.highPower);
                }

                if (def.poweredReservable)
                {
                    RegisterExternalReservable(namedDef.defName, def.lowPower, def.highPower);
                }

                if (def.scheduledPower)
                {
                    RegisterScheduledBuilding(namedDef.defName, def.lowPower, def.highPower);
                }

                // Some objects might not be reservable, like workbenches.
                // e.g., HydroponicsBasins
                if (!def.poweredWorkbench && !def.poweredReservable && !def.scheduledPower)
                {
                    powerLevels.Add(namedDef.defName, new Vector2(def.lowPower, def.highPower));
                }
            }
        }

        private void UpdateTurnItOnandOffDefs()
        {
            var defs = DefDatabase<TurnItOnandOffDef>.AllDefs;
            foreach (var def in defs)
            {
                var target = def.targetDef;
                var namedDef = DefDatabase<ThingDef>.GetNamedSilentFail(target);
                if (namedDef == null)
                {
                    continue;
                }

                if (def.poweredWorkbench)
                {
                    RegisterWorkTable(namedDef.defName, def.lowPower, def.highPower);
                }

                if (def.poweredReservable)
                {
                    RegisterExternalReservable(namedDef.defName, def.lowPower, def.highPower);
                }
            }
        }

        private void UpdateDefinitions()
        {
            if (Prefs.DevMode)
            {
                Verse.Log.Message("Clearing power-levels");
            }

            powerLevels = new Dictionary<string, Vector2>();
            UpdateRepowerDefs();
            UpdateTurnItOnandOffDefs();
            var lowPower = -10f;
            if (lowValue != null)
            {
                lowPower = lowValue.Value * -1;
            }

            var highPowerMultiplier = 2.5f;
            if (highMultiplier != null)
            {
                highPowerMultiplier = highMultiplier.Value;
            }

            var repowerVanilla = new List<string[]>
            {
                new[] {"ElectricCrematorium", "200", "750", "Normal"},
                new[] {"ElectricSmelter", "400", "4500", "Normal"},
                new[] {"HiTechResearchBench", "100", "1000", "Normal"},
                new[] {"HydroponicsBasin", "5", "75", "Special"},
                //new string[] { "SunLamp", "0", "2900", "Special" },
                new[] {"Autodoor", "5", "500", "Special"}
            };
            var specialCases = new List<string>
            {
                "MultiAnalyzer",
                "VitalsMonitor",
                "DeepDrill"
            };
            foreach (var tv in from tvDef in DefDatabase<ThingDef>.AllDefsListForReading
                where tvDef.building?.joyKind == DefDatabase<JoyKindDef>.GetNamed("Television")
                select tvDef)
            {
                specialCases.Add(tv.defName);
            }

            if (!applyRepowerVanilla)
            {
                repowerVanilla = new List<string[]>
                {
                    //new string[] { "SunLamp", "0", "2900", "Special" },
                    new[] {"Autodoor", "5", "500", "Special"}
                };
                specialCases.Add("HiTechResearchBench");
            }


            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                CompProperties_Power powerProps;
                if ((from stringArray in repowerVanilla where stringArray[0] == def.defName select stringArray).Any())
                {
                    var repowerSetting = (from stringArray in repowerVanilla
                        where stringArray[0] == def.defName
                        select stringArray).First();
                    if (repowerSetting[3] == "Normal")
                    {
                        RegisterWorkTable(def.defName, -Convert.ToInt32(repowerSetting[1]),
                            -Convert.ToInt32(repowerSetting[2]));
                    }
                    else
                    {
                        RegisterSpecialPowerTrader(def.defName, -Convert.ToInt32(repowerSetting[1]),
                            -Convert.ToInt32(repowerSetting[2]));
                    }

                    continue;
                }

                if (specialCases.Contains(def.defName))
                {
                    powerProps = def.GetCompProperties<CompProperties_Power>();
                    RegisterSpecialPowerTrader(def.defName, lowPower,
                        powerProps.basePowerConsumption * highPowerMultiplier * -1);
                    continue;
                }

                if (powerLevels.ContainsKey(def.defName))
                {
                    continue;
                }

                if (!typeof(Building_WorkTable).IsAssignableFrom(def.thingClass))
                {
                    continue;
                }

                powerProps = def.GetCompProperties<CompProperties_Power>();
                if (powerProps == null || !typeof(CompPowerTrader).IsAssignableFrom(powerProps.compClass))
                {
                    continue;
                }

                RegisterWorkTable(def.defName, lowPower, powerProps.basePowerConsumption * highPowerMultiplier * -1);
            }

            Logger.Message("Initialized Components");

            medicalBedDef = ThingDef.Named("HospitalBed");
            HiTechResearchBenchDef = ThingDef.Named("HiTechResearchBench");
            AutodoorDef = ThingDef.Named("Autodoor");
            DeepDrillDef = ThingDef.Named("DeepDrill");
            HydroponicsBasinDef = ThingDef.Named("HydroponicsBasin");
        }

        private static void RegisterWorkTable(string defName, float idlePower, float activePower)
        {
            if (Prefs.DevMode)
            {
                Verse.Log.Message($"adding {defName}, low: {idlePower}, high: {activePower}");
            }

            powerLevels.Add(defName, new Vector2(idlePower, activePower));
        }

        private static void RegisterSpecialPowerTrader(string defName, float idlePower, float activePower)
        {
            if (powerLevels.ContainsKey(defName))
            {
                return;
            }

            if (Prefs.DevMode)
            {
                Verse.Log.Message($"adding {defName}, low: {idlePower}, high: {activePower}");
            }

            powerLevels.Add(defName, new Vector2(idlePower, activePower));
        }

        private void RegisterScheduledBuilding(string defName, int lowPower, int highPower)
        {
            if (defName == null)
            {
                Logger.Warning("Defname is null");
                return;
            }

            try
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                RegisterWorkTable(defName, lowPower, highPower);
                ScheduledBuildingsDefs.Add(def);
            }
            catch (Exception e)
            {
                Logger.Error($"Error while registering a scheduled building: {e.Message}");
            }
        }

        public static float PowerFactor(Building building)
        {
            var defName = building.def.defName;

            if (!powerLevels.ContainsKey(defName))
            {
                return 1;
            }

            var inUse = buildingsThatWereUsedLastTick.Contains(building);
            instance.Logger.Message($"{building.ThingID} ({defName}) power adjusted");
            return powerLevels[defName][inUse ? 1 : 0];
        }

        #region tracking

        private static int inUseTick;
        private static readonly HashSet<Building> buildingsThatWereUsedLastTick = new HashSet<Building>();
        private static readonly HashSet<Building> buildingsInUseThisTick = new HashSet<Building>();
        public static readonly HashSet<Building> buildingsToModifyPowerOn = new HashSet<Building>();

        private static readonly HashSet<ThingDef> buildingDefsReservable = new HashSet<ThingDef>();
        private static readonly HashSet<Building> reservableBuildings = new HashSet<Building>();

        private readonly HashSet<ThingDef> ScheduledBuildingsDefs = new HashSet<ThingDef>();
        private readonly HashSet<Building> scheduledBuildings = new HashSet<Building>();

        private static readonly HashSet<Building_Bed> MedicalBeds = new HashSet<Building_Bed>();
        private static readonly HashSet<Building> HiTechResearchBenches = new HashSet<Building>();

        private static readonly HashSet<Building_Door> Autodoors = new HashSet<Building_Door>();
        private static readonly HashSet<Building> DeepDrills = new HashSet<Building>();
        private static readonly HashSet<Building> HydroponcsBasins = new HashSet<Building>();

        private static ThingDef medicalBedDef;
        private static ThingDef HiTechResearchBenchDef;
        private static ThingDef AutodoorDef;
        private static ThingDef DeepDrillDef;
        private static ThingDef HydroponicsBasinDef;

        public static void AddBuildingUsed(Building building)
        {
            buildingsInUseThisTick.Add(building);
        }

        private static void RegisterExternalReservable(string defName, int lowPower, int highPower)
        {
            try
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);

                if (defName == null)
                {
                    instance.Logger.Message(
                        "Defname could not be found, it's respective mod probably isn't loaded");
                    return;
                }

                instance.Logger.Message($"Attempting to register def named {defName}");

                RegisterWorkTable(defName, lowPower, highPower);
                buildingDefsReservable.Add(def);
            }
            catch (Exception e)
            {
                instance.Logger.Message(e.Message);
            }
        }

        private static void ScanExternalReservable()
        {
            reservableBuildings.Clear();
            foreach (var def in buildingDefsReservable)
            {
                foreach (var map in Find.Maps)
                {
                    if (map == null)
                    {
                        continue;
                    }

                    var buildings = map.listerBuildings.AllBuildingsColonistOfDef(def);
                    foreach (var building in buildings)
                    {
                        if (building == null)
                        {
                            continue;
                        }

                        reservableBuildings.Add(building);
                    }
                }
            }
        }

        public static void EvalExternalReservable()
        {
            foreach (var building in reservableBuildings)
            {
                // Cache misses
                if (building?.Map == null)
                {
                    continue;
                }

                if (building.Map.reservationManager.IsReservedByAnyoneOf(building, building.Faction))
                {
                    buildingsInUseThisTick.Add(building);
                }
            }
        }

        // Evaluate medical beds for medical beds in use, to register that the vitals monitors should be in high power mode
        private static void EvalBeds()
        {
            foreach (var mediBed in MedicalBeds)
            {
                if (mediBed?.Map == null)
                {
                    continue;
                }

                var occupied = false;
                foreach (var unused in mediBed.CurOccupants)
                {
                    occupied = true;
                }

                if (!occupied)
                {
                    continue;
                }

                var facilityAffector = mediBed.GetComp<CompAffectedByFacilities>();
                foreach (var facility in facilityAffector.LinkedFacilitiesListForReading)
                {
                    buildingsInUseThisTick.Add(facility as Building);
                }
            }
        }

        private static void EvalDeepDrills()
        {
            foreach (var deepDrill in DeepDrills)
            {
                if (deepDrill?.Map == null)
                {
                    continue;
                }

                var inUse = deepDrill.Map.reservationManager.IsReservedByAnyoneOf(deepDrill, deepDrill.Faction);

                if (!inUse)
                {
                    continue;
                }

                buildingsInUseThisTick.Add(deepDrill);
            }
        }

        // How to tell if a research table is in use?
        // I can't figure it out. Instead let's base it on being reserved for use
        private static void EvalResearchTables()
        {
            foreach (var researchTable in HiTechResearchBenches)
            {
                if (researchTable?.Map == null)
                {
                    continue;
                }

                // Determine if we are reserved:
                var inUse = researchTable.Map.reservationManager.IsReservedByAnyoneOf(researchTable,
                    researchTable.Faction);

                if (!inUse)
                {
                    continue;
                }

                buildingsInUseThisTick.Add(researchTable);
                var facilityAffector = researchTable.GetComp<CompAffectedByFacilities>();
                foreach (var facility in facilityAffector.LinkedFacilitiesListForReading)
                {
                    buildingsInUseThisTick.Add(facility as Building);
                }
            }
        }

        private void EvalScheduledBuildings()
        {
            foreach (var building in scheduledBuildings)
            {
                if (building?.Map == null)
                {
                    continue;
                }

                var comp = building.GetComp<CompSchedule>();
                if (comp == null)
                {
                    continue; // Doesn't actually have a schedule
                }

                if (comp.Allowed)
                {
                    buildingsInUseThisTick.Add(building);
                }
            }
        }

        private static void EvalAutodoors()
        {
            foreach (var autodoor in Autodoors)
            {
                if (autodoor?.Map == null)
                {
                    continue;
                }

                // If the door allows passage and isn't blocked by an object
                var inUse = autodoor.Open && !autodoor.BlockedOpenMomentary;
                if (inUse)
                {
                    buildingsInUseThisTick.Add(autodoor);
                }
            }
        }

        private void EvalHydroponicsBasins()
        {
            if (ModLister.GetActiveModWithIdentifier("Aidan.SelfLitHydroponics") != null)
            {
                return;
            }

            foreach (var basin in HydroponcsBasins)
            {
                if (basin?.Map == null)
                {
                    continue;
                }

                foreach (var tile in basin.OccupiedRect())
                {
                    var thingsOnTile = basin.Map.thingGrid.ThingsListAt(tile);
                    foreach (var thing in thingsOnTile)
                    {
                        if (!(thing is Plant))
                        {
                            continue;
                        }

                        buildingsInUseThisTick.Add(basin);
                        break;
                    }
                }
            }
        }

        private static HashSet<ThingDef> thingDefsToLookFor;

        private static void ScanForThings()
        {
            // Build the set of def names to look for if we don't have it
            if (thingDefsToLookFor == null)
            {
                thingDefsToLookFor = new HashSet<ThingDef>();
                var defNames = powerLevels.Keys;
                foreach (var defName in defNames)
                {
                    thingDefsToLookFor.Add(ThingDef.Named(defName));
                }
            }

            ScanExternalReservable(); // Handle the scanning of external reservable objects

            buildingsToModifyPowerOn.Clear();
            MedicalBeds.Clear();
            HiTechResearchBenches.Clear();
            Autodoors.Clear();
            DeepDrills.Clear();
            HydroponcsBasins.Clear();

            var maps = Find.Maps;
            foreach (var map in maps)
            {
                foreach (var def in thingDefsToLookFor)
                {
                    var matchingThings = map.listerBuildings.AllBuildingsColonistOfDef(def);
                    // Merge in all matching things
                    buildingsToModifyPowerOn.UnionWith(matchingThings);
                }

                // Register the medical beds in the watch list
                var mediBeds = map.listerBuildings.AllBuildingsColonistOfDef(medicalBedDef);
                foreach (var mediBed in mediBeds)
                {
                    var medicalBed = mediBed as Building_Bed;
                    MedicalBeds.Add(medicalBed);
                }

                // Register High tech research tables too
                var researchTables = map.listerBuildings.AllBuildingsColonistOfDef(HiTechResearchBenchDef);
                HiTechResearchBenches.UnionWith(researchTables);

                var doors = map.listerBuildings.AllBuildingsColonistOfDef(AutodoorDef);
                foreach (var door in doors)
                {
                    var autodoor = door as Building_Door;
                    Autodoors.Add(autodoor);
                }

                var deepDrills = map.listerBuildings.AllBuildingsColonistOfDef(DeepDrillDef);
                DeepDrills.UnionWith(deepDrills);

                var hydroponicsBasins = map.listerBuildings.AllBuildingsColonistOfDef(HydroponicsBasinDef);
                HydroponcsBasins.UnionWith(hydroponicsBasins);
            }
        }

        #endregion
    }
}