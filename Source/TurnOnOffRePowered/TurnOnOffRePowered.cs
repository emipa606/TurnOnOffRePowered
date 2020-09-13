using System;
using RimWorld;
using Verse;
using System.Collections.Generic;
using UnityEngine;
using HugsLib;
using HarmonyLib;
using HugsLib.Settings;

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

    public class TurnItOnandOff : ModBase
    {
        private SettingHandle<float> lowValue;
        private SettingHandle<float> highMultiplier;
        public override string ModIdentifier
        {
            get
            {
                return "TurnOnOffRePowered";
            }
        }

        int lastVisibleBuildings = 0;

        int ticksToRescan = 0;

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

            foreach (Thing thing in buildingsToModifyPowerOn)
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

            foreach (Building building in buildingsThatWereUsedLastTick)
            {
                if (!buildingsToModifyPowerOn.Contains(building)) continue;

                var powerComp = building.TryGetComp<CompPowerTrader>();
                if (powerComp != null)
                {
                    powerComp.PowerOutput = powerLevels[building.def.defName][1];
                }
            }
        }

        public static TurnItOnandOff instance;
        public static void Log(string log)
        {
            if (instance == null) return;
            instance.Logger.Message(log);
        }

        public override void SettingsChanged()
        {
            base.SettingsChanged();
            UpdateDefinitions();
        }

        public override void DefsLoaded()
        {
            lowValue = Settings.GetHandle<float>("lowValue", "lowValue.label".Translate(), "lowValue.tooltip".Translate(), 10f, Validators.FloatRangeValidator(1f, 100f));
            highMultiplier = Settings.GetHandle<float>("highMultiplier", "highMultiplier.label".Translate(), "highMultiplier.tooltip".Translate(), 2.5f, Validators.FloatRangeValidator(0.1f, 10f));

            UpdateDefinitions();
        }

        public override void Initialize()
        {
            instance = this;

            Logger.Message("Registered instance");
        }

        private void UpdateRepowerDefs()
        {
            var defs = DefDatabase<RePower.RePowerDef>.AllDefs;

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
                    RegisterWorkTable(namedDef.defName, def.lowPower, def.highPower);
                if (def.poweredReservable)
                    RegisterExternalReservable(namedDef.defName, def.lowPower, def.highPower);
            }
        }

        private void UpdateDefinitions()
        {
            if (Prefs.DevMode)
                Verse.Log.Message($"Clearing power-levels");
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
            var specialCases = new List<string>
            {
                "MultiAnalyzer",
                "VitalsMonitor",
                "HiTechResearchBench",
                "TubeTelevision",
                "FlatscreenTelevision",
                "MegascreenTelevision",
                "DeepDrill"
            };

            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                CompProperties_Power powerProps;
                if (specialCases.Contains(def.defName))
                {
                    powerProps = def.GetCompProperties<CompProperties_Power>();
                    RegisterSpecialPowerTrader(def.defName, lowPower, powerProps.basePowerConsumption * highPowerMultiplier * -1);
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

        // Power levels pairs as Vector2's, X = Idling, Y = In Use
        static Dictionary<string, Vector2> powerLevels = new Dictionary<string, Vector2>();

        static void RegisterWorkTable(string defName, float idlePower, float activePower)
        {
            if (Prefs.DevMode)
                Verse.Log.Message($"adding {defName}, low: {idlePower}, high: {activePower}");
            powerLevels.Add(defName, new Vector2(idlePower, activePower));
        }

        static void RegisterSpecialPowerTrader(string defName, float idlePower, float activePower)
        {
            if (!powerLevels.ContainsKey(defName))
            {
                if (Prefs.DevMode)
                    Verse.Log.Message($"adding {defName}, low: {idlePower}, high: {activePower}");
                powerLevels.Add(defName, new Vector2(idlePower, activePower));
            }
        }

        void RegisterScheduledBuilding(string defName, int lowPower, int highPower)
        {
            if (defName == null)
            {
                Logger.Warning($"Def Named {defName} could not be found");
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

        static public float PowerFactor(CompPowerTrader trader, Building building)
        {
            var defName = building.def.defName;

            if (powerLevels.ContainsKey(defName))
            {
                bool inUse = buildingsThatWereUsedLastTick.Contains(building);
                instance.Logger.Message(string.Format("{0} ({1}) power adjusted", building.ThingID, defName));
                return powerLevels[defName][inUse ? 1 : 0];
            }

            return 1;
        }

        #region tracking
        public static int inUseTick = 0;
        public static HashSet<Building> buildingsThatWereUsedLastTick = new HashSet<Building>();
        public static HashSet<Building> buildingsInUseThisTick = new HashSet<Building>();
        public static HashSet<Building> buildingsToModifyPowerOn = new HashSet<Building>();

        public static HashSet<ThingDef> buildingDefsReservable = new HashSet<ThingDef>();
        public static HashSet<Building> reservableBuildings = new HashSet<Building>();

        public HashSet<ThingDef> ScheduledBuildingsDefs = new HashSet<ThingDef>();
        public HashSet<Building> ScheduledBuildings = new HashSet<Building>();

        public static HashSet<Building_Bed> MedicalBeds = new HashSet<Building_Bed>();
        public static HashSet<Building> HiTechResearchBenches = new HashSet<Building>();

        public static HashSet<Building_Door> Autodoors = new HashSet<Building_Door>();
        public static HashSet<Building> DeepDrills = new HashSet<Building>();
        public static HashSet<Building> HydroponcsBasins = new HashSet<Building>();

        private static ThingDef medicalBedDef;
        private static ThingDef HiTechResearchBenchDef;
        private static ThingDef AutodoorDef;
        private static ThingDef DeepDrillDef;
        private static ThingDef HydroponicsBasinDef;

        public static void AddBuildingUsed(Building building)
        {
            buildingsInUseThisTick.Add(building);
        }

        public static void RegisterExternalReservable(string defName, int lowPower, int highPower)
        {
            try
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);

                if (defName == null)
                {
                    instance.Logger.Message($"Def Named {defName} could not be found, it's respective mod probably isn't loaded");
                    return;
                }
                else
                {
                    instance.Logger.Message($"Attempting to register def named {defName}");
                }

                RegisterWorkTable(defName, lowPower, highPower);
                buildingDefsReservable.Add(def);
            }
            catch (System.Exception e)
            {
                instance.Logger.Message(e.Message);
            }
        }

        public static void ScanExternalReservable()
        {
            reservableBuildings.Clear();
            foreach (ThingDef def in buildingDefsReservable)
            {
                foreach (var map in Find.Maps)
                {
                    if (map == null) continue;
                    var buildings = map.listerBuildings.AllBuildingsColonistOfDef(def);
                    foreach (var building in buildings)
                    {
                        if (building == null) continue;
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
                if (building?.Map == null) continue;

                if (building.Map.reservationManager.IsReservedByAnyoneOf(building, building.Faction))
                {
                    buildingsInUseThisTick.Add(building);
                }
            }
        }

        // Evaluate medical beds for medical beds in use, to register that the vitals monitors should be in high power mode
        public static void EvalBeds()
        {
            foreach (var mediBed in MedicalBeds)
            {
                if (mediBed?.Map == null) continue;

                bool occupied = false;
                foreach (var occupant in mediBed.CurOccupants)
                {
                    occupied = true;
                }

                if (!occupied) continue;
                var facilityAffector = mediBed.GetComp<CompAffectedByFacilities>();
                foreach (var facility in facilityAffector.LinkedFacilitiesListForReading)
                {
                    buildingsInUseThisTick.Add(facility as Building);
                }
            }
        }

        public static void EvalDeepDrills()
        {
            foreach (var deepDrill in DeepDrills)
            {
                if (deepDrill?.Map == null) continue;

                var inUse = deepDrill.Map.reservationManager.IsReservedByAnyoneOf(deepDrill, deepDrill.Faction);

                if (!inUse) continue;

                buildingsInUseThisTick.Add(deepDrill);
            }
        }

        // How to tell if a research table is in use?
        // I can't figure it out. Instead let's base it on being reserved for use
        public static void EvalResearchTables()
        {
            foreach (var researchTable in HiTechResearchBenches)
            {
                if (researchTable?.Map == null) continue;

                // Determine if we are reserved:
                var inUse = researchTable.Map.reservationManager.IsReservedByAnyoneOf(researchTable, researchTable.Faction);

                if (!inUse) continue;

                buildingsInUseThisTick.Add(researchTable);
                var facilityAffector = researchTable.GetComp<CompAffectedByFacilities>();
                foreach (var facility in facilityAffector.LinkedFacilitiesListForReading)
                {
                    buildingsInUseThisTick.Add(facility as Building);
                }
            }
        }

        public void EvalScheduledBuildings()
        {
            foreach (var building in ScheduledBuildings)
            {
                if (building == null) continue;
                if (building.Map == null) continue;

                var comp = building.GetComp<CompSchedule>();
                if (comp == null) continue; // Doesn't actually have a schedule

                if (comp.Allowed)
                {
                    buildingsInUseThisTick.Add(building);
                }
            }
        }

        public static void EvalAutodoors()
        {
            foreach (var autodoor in Autodoors)
            {
                if (autodoor == null) continue;
                if (autodoor.Map == null) continue;

                // If the door allows passage and isn't blocked by an object
                var inUse = autodoor.Open && (!autodoor.BlockedOpenMomentary);
                if (inUse) buildingsInUseThisTick.Add(autodoor);
            }
        }

        public void EvalHydroponicsBasins()
        {
            foreach (var basin in HydroponcsBasins)
            {
                if (basin == null) continue;
                if (basin.Map == null) continue;

                foreach (var tile in basin.OccupiedRect())
                {
                    var thingsOnTile = basin.Map.thingGrid.ThingsListAt(tile);
                    foreach (var thing in thingsOnTile)
                    {
                        if (thing is Plant)
                        {
                            buildingsInUseThisTick.Add(basin);
                            break;
                        }
                    }
                }
            }
        }

        public static HashSet<ThingDef> thingDefsToLookFor;
        public static void ScanForThings()
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
            foreach (Map map in maps)
            {
                foreach (ThingDef def in thingDefsToLookFor)
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

                // Register Hightech research tables too
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