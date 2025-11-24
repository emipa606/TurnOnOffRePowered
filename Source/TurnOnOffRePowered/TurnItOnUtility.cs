using HarmonyLib;
using RePower;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TurnOnOffRePowered;

// Utility class: moved most static fields / methods from original ModBase here.
public static class TurnItOnUtility
{
    private static readonly HashSet<ThingDef> AutodoorDefs = new();
    private static readonly HashSet<Building> Autodoors = new();
    private static readonly HashSet<ThingDef> buildingDefsReservable = new();

    private static readonly MethodInfo canTryCloseAutomaticallyMethod =
            AccessTools.PropertyGetter(typeof(Building_Door), "CanTryCloseAutomatically");

    private static ThingDef DeepDrillDef;
    private static readonly HashSet<Building> DeepDrills = new();
    private static ThingDef HiTechResearchBenchDef;
    private static readonly HashSet<Building> HiTechResearchBenches = new();
    private static readonly HashSet<Building> HydroponcsBasins = new();
    private static ThingDef HydroponicsBasinDef;
    private static ThingDef medicalBedDef;
    private static readonly HashSet<Building_Bed> MedicalBeds = new();
    private static readonly HashSet<Building> reservableBuildings = new();
    private static List<ThingDef> rimfactoryAssemblerDefs = new();
    private static readonly HashSet<Building> RimfactoryBuildings = new();

    private static bool rimfactoryIsLoaded;
    private static readonly HashSet<Building> Scanners = new();

    // scheduled/scheduled-defs storage (was instance members originally)
    private static readonly HashSet<Building> scheduledBuildings = new();
    private static readonly HashSet<ThingDef> ScheduledBuildingsDefs = new();
    private static bool selfLitHydroponicsIsLoaded;
    private static HashSet<ThingDef> thingDefsToLookFor;
    private static readonly HashSet<Building_Turret> Turrets = new();
    public static readonly List<string> AlwaysIgnored = new() { "Furnace" };
    public static readonly List<Type> AlwaysIgnoredClass = new() { typeof(Building_MechGestator) };
    public static readonly HashSet<Building> buildingsInUseThisTick = new();
    public static readonly HashSet<Building> buildingsThatWereUsedLastTick = new();
    // exposed collections so existing patches that referenced TurnOnOffRePowered.* can be repointed easily
    public static readonly HashSet<Building> buildingsToModifyPowerOn = new();

    // public access for patches to call
    public static readonly HashSet<Building> buildingsThatExternalCodeMightQuery = buildingsToModifyPowerOn;

    // Power levels pairs as Vector2's, X = Idling, Y = In Use
    public static Dictionary<string, Vector2> powerLevels = new();

    private static bool isDoorType(ThingDef def)
    {
        if(typeof(Building_Door).IsAssignableFrom(def.thingClass))
        {
            return true;
        }

        return def.thingClass.FullName == "DoorsExpanded.Building_DoorRemote";
    }

    private static void registerExternalReservable(string defName, int lowPower, int highPower)
    {
        try
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if(def == null)
            {
                LogMessage("Defname could not be found, its respective mod probably isn't loaded");
                return;
            }

            LogMessage($"Attempting to register def named {defName}");
            registerPowerUserBuilding(defName, lowPower, highPower);
            buildingDefsReservable.Add(def);
        } catch(Exception e)
        {
            Log.Error(e.ToString());
        }
    }

    // Helper: register power-levels
    private static void registerPowerUserBuilding(string defName, float idlePower, float activePower)
    {
        var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
        if(def == null)
        {
            return;
        }

        LogMessage($"adding {def.label.CapitalizeFirst()}, low: {idlePower}, high: {activePower}");
        powerLevels[defName] = new Vector2(idlePower, activePower);
    }

    private static void registerSpecialPowerTrader(string defName, float idlePower, float activePower)
    {
        if(powerLevels.ContainsKey(defName))
        {
            return;
        }

        var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
        if(def == null)
        {
            return;
        }

        LogMessage($"adding special {def.label.CapitalizeFirst()}, low: {idlePower}, high: {activePower}");
        powerLevels[defName] = new Vector2(idlePower, activePower);
    }

    private static void ScanExternalReservable()
    {
        reservableBuildings.Clear();
        foreach(var def in buildingDefsReservable)
        {
            foreach(var map in Find.Maps)
            {
                var buildings = map.listerBuildings.AllBuildingsColonistOfDef(def);
                foreach(var building in buildings)
                {
                    reservableBuildings.Add(building);
                }
            }
        }
    }

    // The big re-build entry that mirrors original updateDefinitions()
    private static void UpdateDefinitions()
    {
        LogMessage("Clearing power-levels");
        powerLevels = new Dictionary<string, Vector2>();

        updateRepowerDefs();
        UpdateTurnItOnAndOffDefs();

        var settings = TurnOnOffMod.Instance?.Settings;
        var lowPower = -10f;
        if(settings != null)
        {
            lowPower = settings.lowValue * -1f;
        }

        var highPowerMultiplier = settings?.highMultiplier ?? 2.5f;
        if(highPowerMultiplier <= 0)
        {
            highPowerMultiplier = 0.001f;
        }

        var doorPowerMultiplier = settings?.doorMultiplier ?? 10f;
        if(doorPowerMultiplier <= 0)
        {
            doorPowerMultiplier = 0.001f;
        }

        var repowerVanilla = new List<string[]>
        {
            new[] { "ElectricCrematorium", "200", "750", "Normal" },
            new[] { "ElectricSmelter", "400", "4500", "Normal" },
            new[] { "HiTechResearchBench", "100", "1000", "Normal" },
            new[] { "HydroponicsBasin", "5", "75", "Special" }
        };
        var specialCases = new List<string> { "MultiAnalyzer", "VitalsMonitor", "DeepDrill" };

        foreach(var tvDef in DefDatabase<ThingDef>.AllDefsListForReading
            .Where(tvDef => tvDef.building?.joyKind == DefDatabase<JoyKindDef>.GetNamed("Television")))
        {
            specialCases.Add(tvDef.defName);
        }

        if(settings != null && !settings.applyRepowerVanilla)
        {
            repowerVanilla = new List<string[]>();
            specialCases.Add("HiTechResearchBench");
        }

        foreach(var def in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if(AlwaysIgnored.Contains(def.defName) || AlwaysIgnoredClass.Contains(def.thingClass))
            {
                LogMessage($"Ignoring {def.LabelCap}");
                continue;
            }

            if(repowerVanilla.Any(a => a[0] == def.defName))
            {
                var repowerSetting = repowerVanilla.First(a => a[0] == def.defName);
                if(repowerSetting[3] == "Normal")
                {
                    registerPowerUserBuilding(
                        def.defName,
                        -Convert.ToInt32(repowerSetting[1]),
                        -Convert.ToInt32(repowerSetting[2]));
                } else
                {
                    registerSpecialPowerTrader(
                        def.defName,
                        -Convert.ToInt32(repowerSetting[1]),
                        -Convert.ToInt32(repowerSetting[2]));
                }

                continue;
            }

            var powerProps = def.GetCompProperties<CompProperties_Power>();
            if(powerProps == null || !typeof(CompPowerTrader).IsAssignableFrom(powerProps.compClass))
            {
                continue;
            }

            if(powerLevels.ContainsKey(def.defName))
            {
                continue;
            }

            if(specialCases.Contains(def.defName))
            {
                registerSpecialPowerTrader(
                    def.defName,
                    lowPower,
                    powerProps.PowerConsumption * highPowerMultiplier * -1);
                continue;
            }

            if(!typeof(Building_WorkTable).IsAssignableFrom(def.thingClass) &&
                !typeof(Building_Turret).IsAssignableFrom(def.thingClass) &&
                !isDoorType(def) &&
                !def.comps.Any(comp => comp.GetType().IsSubclassOf(typeof(CompProperties_Scanner))) &&
                !rimfactoryAssemblerDefs.Contains(def))
            {
                continue;
            }

            if(isDoorType(def))
            {
                AutodoorDefs.Add(def);
                registerSpecialPowerTrader(
                    def.defName,
                    lowPower,
                    powerProps.PowerConsumption * doorPowerMultiplier * -1);
                continue;
            }

            registerPowerUserBuilding(def.defName, lowPower, powerProps.PowerConsumption * highPowerMultiplier * -1);
        }

        powerLevels.Remove("FM_AIManager");

        LogMessage("Initialized Components");

        medicalBedDef = ThingDef.Named("HospitalBed");
        HiTechResearchBenchDef = ThingDef.Named("HiTechResearchBench");
        DeepDrillDef = ThingDef.Named("DeepDrill");
        HydroponicsBasinDef = ThingDef.Named("HydroponicsBasin");
        ScanForThings();
    }

    private static void updateRepowerDefs()
    {
        var defs = DefDatabase<RePowerDef>.AllDefs;
        foreach(var def in defs)
        {
            var targetDef = def.targetDef;
            var namedDef = DefDatabase<ThingDef>.GetNamedSilentFail(targetDef);
            if(namedDef == null)
            {
                continue;
            }

            if(def.poweredWorkbench)
            {
                registerPowerUserBuilding(namedDef.defName, def.lowPower, def.highPower);
            }

            if(def.poweredReservable)
            {
                registerExternalReservable(namedDef.defName, def.lowPower, def.highPower);
            }

            if(def.scheduledPower)
            {
                ScheduledBuildingsDefs.Add(namedDef);
            }

            if(!def.poweredWorkbench && !def.poweredReservable && !def.scheduledPower)
            {
                powerLevels[namedDef.defName] = new Vector2(def.lowPower, def.highPower);
            }
        }
    }

    private static void UpdateTurnItOnAndOffDefs()
    {
        var defs = DefDatabase<TurnItOnandOffDef>.AllDefs;
        foreach(var def in defs)
        {
            var target = def.targetDef;
            var namedDef = DefDatabase<ThingDef>.GetNamedSilentFail(target);
            if(namedDef == null)
            {
                continue;
            }

            if(def.poweredWorkbench)
            {
                registerPowerUserBuilding(namedDef.defName, def.lowPower, def.highPower);
            }

            if(def.poweredReservable)
            {
                registerExternalReservable(namedDef.defName, def.lowPower, def.highPower);
            }
        }
    }

    // Public helper that was originally on ModBase
    public static void AddBuildingUsed(Building building) { buildingsInUseThisTick.Add(building); }

    // Clear variables (keeps parity with original)
    public static void ClearVariables()
    {
        powerLevels = new Dictionary<string, Vector2>();
        buildingsToModifyPowerOn.Clear();
        buildingsThatWereUsedLastTick.Clear();
        buildingsInUseThisTick.Clear();
        buildingDefsReservable.Clear();
        reservableBuildings.Clear();
        ScheduledBuildingsDefs.Clear();
        scheduledBuildings.Clear();
        MedicalBeds.Clear();
        HiTechResearchBenches.Clear();
        Autodoors.Clear();
        RimfactoryBuildings.Clear();
        DeepDrills.Clear();
        Scanners.Clear();
        HydroponcsBasins.Clear();
        Turrets.Clear();
        thingDefsToLookFor = null;
    }

    // This is the old DefsLoaded logic (moved)
    public static void DefsLoaded()
    {
        _ = TurnOnOffMod.Instance?.Settings;
        // Recreate setting-backed values if needed
        rimfactoryIsLoaded = ModLister.GetActiveModWithIdentifier("spdskatr.projectrimfactory", true) != null;
        selfLitHydroponicsIsLoaded = ModLister.GetActiveModWithIdentifier("Aidan.SelfLitHydroponics", true) != null;

        rimfactoryAssemblerDefs = new List<ThingDef>();
        if(rimfactoryIsLoaded)
        {
            LogMessage("Project Rimfactory is loaded");
            rimfactoryAssemblerDefs = DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def.defName.StartsWith("PRF_") && def.thingClass.Name.EndsWith("Assembler"))
                .ToList();
        }

        UpdateDefinitions();
    }

    public static void EvalHydroponicsBasins()
    {
        if(selfLitHydroponicsIsLoaded)
        {
            return;
        }

        foreach(var basin in HydroponcsBasins)
        {
            if(basin?.Map == null)
            {
                continue;
            }

            foreach(var tile in basin.OccupiedRect())
            {
                var thingsOnTile = basin.Map.thingGrid.ThingsListAt(tile);
                foreach(var thing in thingsOnTile)
                {
                    if(thing is not Plant)
                    {
                        continue;
                    }

                    buildingsInUseThisTick.Add(basin);
                    break;
                }
            }
        }
    }

    // Scheduled buildings behaviour moved here
    public static void EvalScheduledBuildings()
    {
        foreach(var building in scheduledBuildings)
        {
            if(building?.Map == null)
            {
                continue;
            }

            var comp = building.GetComp<CompSchedule>();
            if(comp == null)
            {
                continue;
            }

            if(comp.Allowed)
            {
                buildingsInUseThisTick.Add(building);
            }
        }
    }

    public static void EvalTurrets()
    {
        foreach(var turret in Turrets)
        {
            if(turret?.Map == null)
            {
                continue;
            }

            if(turret.CurrentTarget == LocalTargetInfo.Invalid)
            {
                continue;
            }

            buildingsInUseThisTick.Add(turret);
        }
    }

    // The following methods are moved verbatim (small renames to public)
    public static void EvaluateAutodoors()
    {
        foreach(var autodoor in Autodoors)
        {
            if(autodoor?.Map == null)
            {
                continue;
            }

            if(autodoor is Building_Door buildingDoor)
            {
                var classToCheck = autodoor.def.thingClass;
                if(classToCheck == null)
                {
                    continue;
                }

                var canTryCloseAutomatically = (bool)canTryCloseAutomaticallyMethod.Invoke(buildingDoor, null);
                if(buildingDoor.Open &&
                    !buildingDoor.BlockedOpenMomentary &&
                    (!buildingDoor.HoldOpen && canTryCloseAutomatically || buildingDoor.TicksTillFullyOpened > 0))
                {
                    buildingsInUseThisTick.Add(autodoor);
                }

                continue;
            }

            if(autodoor.def.thingClass.FullName == "DoorsExpanded.Building_DoorRemote")
            {
                var openState = (bool)autodoor.def.thingClass
                    .InvokeMember(
                        "Open",
                        BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                        null,
                        autodoor,
                        null);

                var blockedOpenMomentaryState = (bool)autodoor.def.thingClass
                    .InvokeMember(
                        "BlockedOpenMomentary",
                        BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                        null,
                        autodoor,
                        null);
                var holdOpenRemotelyState = (bool)autodoor.def.thingClass
                    .InvokeMember(
                        "HoldOpenRemotely",
                        BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                        null,
                        autodoor,
                        null);
                var ticksTillFullyOpenedState = (int)autodoor.def.thingClass
                    .InvokeMember(
                        "TicksTillFullyOpened",
                        BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                        null,
                        autodoor,
                        null);

                if(openState && !blockedOpenMomentaryState && !(holdOpenRemotelyState && ticksTillFullyOpenedState == 0))
                {
                    buildingsInUseThisTick.Add(autodoor);
                }
            }
        }
    }

    public static void EvaluateBeds()
    {
        foreach(var medicalBed in MedicalBeds)
        {
            if(medicalBed?.Map == null)
            {
                continue;
            }

            var occupied = false;
            foreach(var unused in medicalBed.CurOccupants)
            {
                occupied = true;
            }

            if(!occupied)
            {
                continue;
            }

            var facilityAffector = medicalBed.GetComp<CompAffectedByFacilities>();
            foreach(var facility in facilityAffector.LinkedFacilitiesListForReading)
            {
                buildingsInUseThisTick.Add(facility as Building);
            }
        }
    }

    public static void EvaluateDeepDrills()
    {
        foreach(var deepDrill in DeepDrills)
        {
            if(deepDrill?.Map == null)
            {
                continue;
            }

            var inUse = deepDrill.Map.reservationManager.IsReservedByAnyoneOf(deepDrill, deepDrill.Faction);
            if(!inUse)
            {
                continue;
            }

            buildingsInUseThisTick.Add(deepDrill);
        }
    }

    public static void EvaluateResearchTables()
    {
        foreach(var researchTable in HiTechResearchBenches)
        {
            if(researchTable?.Map == null)
            {
                continue;
            }

            var inUse = researchTable.Map.reservationManager.IsReservedByAnyoneOf(researchTable, researchTable.Faction);
            if(!inUse)
            {
                continue;
            }

            buildingsInUseThisTick.Add(researchTable);

            var facilityAffector = researchTable.GetComp<CompAffectedByFacilities>();
            foreach(var facility in facilityAffector.LinkedFacilitiesListForReading)
            {
                buildingsInUseThisTick.Add(facility as Building);
            }
        }
    }

    public static void EvaluateRimfactoryWork()
    {
        if(!rimfactoryIsLoaded)
        {
            return;
        }

        foreach(var rimfactoryBuilding in RimfactoryBuildings)
        {
            if(rimfactoryBuilding?.Map == null)
            {
                continue;
            }

            if(buildingsInUseThisTick.Contains(rimfactoryBuilding))
            {
                continue;
            }

            var inspectStringSplitted = rimfactoryBuilding.GetInspectString().Split('\n');
            foreach(var row in inspectStringSplitted)
            {
                if(!row.Contains("(") || row.Contains("W"))
                {
                    continue;
                }

                buildingsInUseThisTick.Add(rimfactoryBuilding);
                break;
            }
        }

        foreach(var building in buildingsToModifyPowerOn)
        {
            if(building?.Map == null)
            {
                continue;
            }

            if(buildingsInUseThisTick.Contains(building))
            {
                continue;
            }

            var interactionSpotBuilding = building.InteractionCell.GetFirstBuilding(building.Map);
            if(interactionSpotBuilding?.def.thingClass.FullName == null)
            {
                continue;
            }

            if(!interactionSpotBuilding.def.thingClass.FullName.StartsWith("ProjectRimFactory"))
            {
                continue;
            }

            if(interactionSpotBuilding.GetInspectString().Contains("%)]"))
            {
                buildingsInUseThisTick.Add(building);
            }
        }
    }

    public static void EvaluateScanners()
    {
        foreach(var scanner in Scanners)
        {
            if(scanner?.Map == null)
            {
                continue;
            }

            var inUse = scanner.Map.reservationManager.IsReservedByAnyoneOf(scanner, scanner.Faction);
            if(!inUse)
            {
                continue;
            }

            buildingsInUseThisTick.Add(scanner);
        }
    }

    // HasEnoughPower moved, but now uses the stored settings
    public static bool HasEnoughPower(Building_WorkTable table)
    {
        var settings = TurnOnOffMod.Instance?.Settings;
        if(settings == null)
        {
            return true;
        }

        if(!settings.blockUseWhenLowPower)
        {
            return true;
        }

        if(!powerLevels.ContainsKey(table.def.defName))
        {
            return true;
        }

        if(table.CanWorkWithoutPower)
        {
            return true;
        }

        if(buildingsThatWereUsedLastTick.Contains(table))
        {
            return true;
        }

        var powerTrader = table.GetComp<CompPowerTrader>();
        if(powerTrader?.PowerNet == null)
        {
            return true;
        }

        var currentPowerNeed = powerLevels[table.def.defName][1] * -1;
        var currentGainRate = powerTrader.PowerNet.CurrentEnergyGainRate() / CompPower.WattsToWattDaysPerTick;
        var currentSavedEnergy = powerTrader.PowerNet.CurrentStoredEnergy() / CompPower.WattsToWattDaysPerTick;

        if(currentGainRate >= currentPowerNeed)
        {
            LogMessage(
                $"Enough power: {table.def.label} requires {currentPowerNeed}, current gainrate: {currentGainRate}");
            return true;
        }

        if(currentSavedEnergy > currentPowerNeed * 500)
        {
            LogMessage(
                $"Enough power: {table.def.label} requires {currentPowerNeed * 500} saved, current saved power: {currentSavedEnergy}");
            return true;
        }

        LogMessage(
            $"Not enough power: {table.def.label} requires {currentPowerNeed} gaining or {currentPowerNeed * 500} saved, current saved power: {currentSavedEnergy}, current gainrate: {currentGainRate}");
        JobFailReason.Is("notEnoughPower.failreason".Translate(currentPowerNeed, "unitOfPower".Translate()));
        return false;
    }

    // Small initializer for any fields that used the Mod instance previously
    public static void InitializeStatic()
    {
        LogMessage("Initializing static utility");
        // nothing else for now
    }

    // For logging
    public static void LogMessage(string message)
    {
        if(TurnOnOffMod.Instance?.Settings != null && !TurnOnOffMod.Instance.Settings.verboseLogging)
        {
            return;
        }
        Log.Message($"[TurnOnOffRePowered]: {message}");
    }

    // Scans across maps to gather buildings matching tracked defs
    public static void ScanForThings()
    {
        if(thingDefsToLookFor == null)
        {
            thingDefsToLookFor = new HashSet<ThingDef>();
            foreach(var defName in powerLevels.Keys)
            {
                var def = ThingDef.Named(defName);
                if(def != null)
                {
                    thingDefsToLookFor.Add(def);
                }
            }
        }

        buildingsToModifyPowerOn.Clear();
        MedicalBeds.Clear();
        HiTechResearchBenches.Clear();
        Autodoors.Clear();
        DeepDrills.Clear();
        Scanners.Clear();
        HydroponcsBasins.Clear();
        Turrets.Clear();

        if(Current.ProgramState != ProgramState.Playing)
        {
            return;
        }

        ScanExternalReservable();

        foreach(var map in Find.Maps)
        {
            foreach(var def in thingDefsToLookFor)
            {
                var matchingThings = map.listerBuildings.AllBuildingsColonistOfDef(def);
                buildingsToModifyPowerOn.UnionWith(matchingThings);
            }

            foreach(var def in AutodoorDefs)
            {
                var autoDoorsFound = map.listerBuildings.AllBuildingsColonistOfDef(def);
                foreach(var building in autoDoorsFound)
                {
                    Autodoors.Add(building);
                }
            }

            if(rimfactoryIsLoaded)
            {
                foreach(var def in rimfactoryAssemblerDefs)
                {
                    var rimfactoryBuildings = map.listerBuildings.AllBuildingsColonistOfDef(def);
                    foreach(var building in rimfactoryBuildings)
                    {
                        RimfactoryBuildings.Add(building);
                    }
                }
            }

            // Medical beds
            var medicalBeds = map.listerBuildings.AllBuildingsColonistOfDef(medicalBedDef);
            foreach(var bed in medicalBeds)
            {
                var mb = bed as Building_Bed;
                if(mb != null)
                {
                    MedicalBeds.Add(mb);
                }
            }

            var researchTables = map.listerBuildings.AllBuildingsColonistOfDef(HiTechResearchBenchDef);
            HiTechResearchBenches.UnionWith(researchTables);

            var turrets = from Building turret in map.listerBuildings.allBuildingsColonist
                where typeof(Building_Turret).IsAssignableFrom(turret.def.thingClass)
                select turret;
            foreach(var building in turrets)
            {
                Turrets.Add(building as Building_Turret);
            }

            var deepDrills = map.listerBuildings.AllBuildingsColonistOfDef(DeepDrillDef);
            DeepDrills.UnionWith(deepDrills);

            var scanners = map.listerBuildings.allBuildingsColonist
                .Where(building => building.AllComps.Any(comp => comp.GetType().IsSubclassOf(typeof(CompScanner))));
            Scanners.UnionWith(scanners);

            var hydroponicsBasins = map.listerBuildings.AllBuildingsColonistOfDef(HydroponicsBasinDef);
            HydroponcsBasins.UnionWith(hydroponicsBasins);
        }
    }
}