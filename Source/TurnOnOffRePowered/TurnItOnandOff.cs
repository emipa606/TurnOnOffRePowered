using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HugsLib;
using HugsLib.Settings;
using RePower;
using RimWorld;
using UnityEngine;
using Verse;

namespace TurnOnOffRePowered;

public class TurnItOnandOff : ModBase
{
    public static readonly HashSet<Building> buildingsToModifyPowerOn = new HashSet<Building>();

    // Power levels pairs as Vector2's, X = Idling, Y = In Use
    public static Dictionary<string, Vector2> powerLevels = new Dictionary<string, Vector2>();

    private static readonly HashSet<ThingDef> AutodoorDefs = new HashSet<ThingDef>();

    private static readonly HashSet<Building> Autodoors = new HashSet<Building>();

    private static readonly HashSet<ThingDef> buildingDefsReservable = new HashSet<ThingDef>();

    private static readonly HashSet<Building> buildingsInUseThisTick = new HashSet<Building>();

    private static readonly HashSet<Building> buildingsThatWereUsedLastTick = new HashSet<Building>();

    private static readonly HashSet<Building> DeepDrills = new HashSet<Building>();

    private static readonly HashSet<Building> Scanners = new HashSet<Building>();

    private static readonly HashSet<Building> HiTechResearchBenches = new HashSet<Building>();

    private static readonly HashSet<Building> HydroponcsBasins = new HashSet<Building>();

    private static readonly HashSet<Building_Bed> MedicalBeds = new HashSet<Building_Bed>();

    private static readonly HashSet<Building> reservableBuildings = new HashSet<Building>();

    private static readonly HashSet<Building_Turret> Turrets = new HashSet<Building_Turret>();

    private static ThingDef DeepDrillDef;

    private static ThingDef HiTechResearchBenchDef;

    private static ThingDef HydroponicsBasinDef;

    private static TurnItOnandOff instance;

    private static int inUseTick;

    private static ThingDef medicalBedDef;

    private static bool rimfactoryIsLoaded;

    private static HashSet<ThingDef> thingDefsToLookFor;

    private static bool selfLitHydroponicsIsLoaded;

    // ReSharper disable once CollectionNeverUpdated.Local
    private readonly HashSet<Building> scheduledBuildings = new HashSet<Building>();

    private readonly HashSet<ThingDef> ScheduledBuildingsDefs = new HashSet<ThingDef>();

    private SettingHandle<bool> applyRepowerVanilla;

    private SettingHandle<float> doorMultiplier;

    private SettingHandle<float> highMultiplier;

    private int lastVisibleBuildings;

    private SettingHandle<float> lowValue;

    private int ticksToRescan;

    private SettingHandle<bool> verboseLogging;

    public override string ModIdentifier => "TurnOnOffRePowered";

    public static void AddBuildingUsed(Building building)
    {
        buildingsInUseThisTick.Add(building);
    }

    public static float PowerFactor(Building building)
    {
        var defName = building.def.defName;

        if (!powerLevels.ContainsKey(defName))
        {
            return 1;
        }

        var inUse = buildingsThatWereUsedLastTick.Contains(building);
        instance.LogMessage($"{building.ThingID} ({defName}) power adjusted");
        return powerLevels[defName][inUse ? 1 : 0];
    }

    public override void DefsLoaded()
    {
        lowValue = Settings.GetHandle(
            "lowValue",
            "lowValue.label".Translate(),
            "lowValue.tooltip".Translate(),
            10f,
            Validators.FloatRangeValidator(1f, 100f));
        highMultiplier = Settings.GetHandle(
            "highMultiplier",
            "highMultiplier.label".Translate(),
            "highMultiplier.tooltip".Translate(),
            2.5f,
            Validators.FloatRangeValidator(0.1f, 10f));
        doorMultiplier = Settings.GetHandle(
            "doorMultiplier",
            "doorMultiplier.label".Translate(),
            "doorMultiplier.tooltip".Translate(),
            10f,
            Validators.FloatRangeValidator(0.1f, 10f));
        applyRepowerVanilla = Settings.GetHandle(
            "applyRepowerVanilla",
            "applyRepowerVanilla.label".Translate(),
            "applyRepowerVanilla.tooltip".Translate(),
            true);
        verboseLogging = Settings.GetHandle(
            "verboseLogging",
            "verboseLogging.label".Translate(),
            "verboseLogging.tooltip".Translate(),
            false);
        rimfactoryIsLoaded = ModLister.GetActiveModWithIdentifier("spdskatr.projectrimfactory") != null;
        selfLitHydroponicsIsLoaded = ModLister.GetActiveModWithIdentifier("Aidan.SelfLitHydroponics") != null;
        if (rimfactoryIsLoaded)
        {
            LogMessage("Project Rimfactory is loaded");
        }

        UpdateDefinitions();
    }

    public override void Initialize()
    {
        instance = this;

        LogMessage("Registered instance");
    }

    public override void SettingsChanged()
    {
        base.SettingsChanged();
        ClearVariables();
        UpdateDefinitions();
    }

    public override void Tick(int currentTick)
    {
        EvaluateRimfactoryWork();
        if (inUseTick == 0)
        {
            inUseTick = currentTick;
            return;
        }

        if (inUseTick != currentTick)
        {
            inUseTick = currentTick;

            buildingsThatWereUsedLastTick.Clear();
            buildingsThatWereUsedLastTick.UnionWith(buildingsInUseThisTick);
            buildingsInUseThisTick.Clear();
        }

        if (Find.CurrentMap == null)
        {
            Log.ErrorOnce("[TurnOnOffRepowered] No home map found, cannot find any colony-owned buildings",
                "TurnOnOffRepowered".GetHashCode());
            return;
        }

        var visibleBuildings = Find.CurrentMap.listerBuildings.allBuildingsColonist.Count;
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

        EvalBeds();
        EvalResearchTables();
        EvalAutodoors();
        EvalDeepDrills();
        EvalScanners();
        EvalHydroponicsBasins();
        EvalTurrets();
        EvalScheduledBuildings();

        foreach (var thing in buildingsToModifyPowerOn)
        {
            if (thing == null)
            {
                LogMessage("Tried to modify power level for thing which no longer exists");
                continue;
            }

            var powerComp = thing.TryGetComp<CompPowerTrader>();
            if (powerComp != null)
            {
                powerComp.PowerOutput = powerLevels[thing.def.defName][0];
            }
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

    private static void EvalAutodoors()
    {
        foreach (var autodoor in Autodoors)
        {
            if (autodoor?.Map == null)
            {
                continue;
            }

            // If the door allows passage and isn't blocked by an object
            if (typeof(Building_Door).IsAssignableFrom(autodoor.def.thingClass))
            {
                var classToCheck = autodoor.def.thingClass;
                if (classToCheck == null)
                {
                    continue;
                }

                var memberFound = classToCheck.GetMember("CanTryCloseAutomatically",
                    BindingFlags.GetProperty | BindingFlags.NonPublic | BindingFlags.Instance);
                while (!memberFound.Any())
                {
                    classToCheck = classToCheck.BaseType;
                    if (classToCheck == null)
                    {
                        break;
                    }

                    memberFound = classToCheck.GetMember("CanTryCloseAutomatically",
                        BindingFlags.GetProperty | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                if (classToCheck == null || !memberFound.Any())
                {
                    continue;
                }

                var canTryCloseAutomatically = (bool)classToCheck.InvokeMember(
                    "CanTryCloseAutomatically",
                    BindingFlags.GetProperty | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    autodoor,
                    null);
                if (((Building_Door)autodoor).Open && !((Building_Door)autodoor).BlockedOpenMomentary &&
                    (!((Building_Door)autodoor).HoldOpen && canTryCloseAutomatically ||
                     ((Building_Door)autodoor).TicksTillFullyOpened > 0))
                {
                    buildingsInUseThisTick.Add(autodoor);
                }

                continue;
            }

            // ReSharper disable once InvertIf
            if (autodoor.def.thingClass.FullName == "DoorsExpanded.Building_DoorRemote")
            {
                var openState = (bool)autodoor.def.thingClass.InvokeMember(
                    "Open",
                    BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                    null,
                    autodoor,
                    null);

                var blockedOpenMomentaryState = (bool)autodoor.def.thingClass.InvokeMember(
                    "BlockedOpenMomentary",
                    BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                    null,
                    autodoor,
                    null);
                var holdOpenRemotelyState = (bool)autodoor.def.thingClass.InvokeMember(
                    "HoldOpenRemotely",
                    BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                    null,
                    autodoor,
                    null);
                var ticksTillFullyOpenedState = (int)autodoor.def.thingClass.InvokeMember(
                    "TicksTillFullyOpened",
                    BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance,
                    null,
                    autodoor,
                    null);

                if (openState && !blockedOpenMomentaryState
                              && !(holdOpenRemotelyState && ticksTillFullyOpenedState == 0))
                {
                    buildingsInUseThisTick.Add(autodoor);
                }
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

    private static void EvalScanners()
    {
        foreach (var scanner in Scanners)
        {
            if (scanner?.Map == null)
            {
                continue;
            }

            var inUse = scanner.Map.reservationManager.IsReservedByAnyoneOf(scanner, scanner.Faction);

            if (!inUse)
            {
                continue;
            }

            buildingsInUseThisTick.Add(scanner);
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
            var inUse = researchTable.Map.reservationManager.IsReservedByAnyoneOf(
                researchTable,
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

    private static void EvaluateRimfactoryWork()
    {
        if (!rimfactoryIsLoaded)
        {
            return;
        }

        foreach (var building in buildingsToModifyPowerOn)
        {
            // Cache misses
            if (building?.Map == null)
            {
                continue;
            }

            if (buildingsInUseThisTick.Contains(building))
            {
                continue;
            }

            var interactionSpotBuilding = building.InteractionCell.GetFirstBuilding(building.Map);

            if (interactionSpotBuilding?.def.thingClass.FullName == null)
            {
                continue;
            }

            if (!interactionSpotBuilding.def.thingClass.FullName.StartsWith("ProjectRimFactory"))
            {
                continue;
            }

            if (interactionSpotBuilding.GetInspectString().Contains("%)]"))
            {
                buildingsInUseThisTick.Add(building);
            }
        }
    }

    private static void RegisterExternalReservable(string defName, int lowPower, int highPower)
    {
        try
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);

            if (defName == null)
            {
                instance.LogMessage("Defname could not be found, it's respective mod probably isn't loaded");
                return;
            }

            instance.LogMessage($"Attempting to register def named {defName}");

            RegisterPowerUserBuilding(defName, lowPower, highPower);
            buildingDefsReservable.Add(def);
        }
        catch (Exception e)
        {
            instance.Logger.Message(e.Message);
        }
    }

    private static void RegisterPowerUserBuilding(string defName, float idlePower, float activePower)
    {
        instance.LogMessage(
            $"adding {DefDatabase<ThingDef>.GetNamedSilentFail(defName).label.CapitalizeFirst()}, low: {idlePower}, high: {activePower}");

        powerLevels.Add(defName, new Vector2(idlePower, activePower));
    }

    private static void RegisterSpecialPowerTrader(string defName, float idlePower, float activePower)
    {
        if (powerLevels.ContainsKey(defName))
        {
            return;
        }

        instance.LogMessage(
            $"adding special {DefDatabase<ThingDef>.GetNamedSilentFail(defName).label.CapitalizeFirst()}, low: {idlePower}, high: {activePower}");

        powerLevels.Add(defName, new Vector2(idlePower, activePower));
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

        buildingsToModifyPowerOn.Clear();
        MedicalBeds.Clear();
        HiTechResearchBenches.Clear();
        Autodoors.Clear();
        DeepDrills.Clear();
        Scanners.Clear();
        HydroponcsBasins.Clear();
        Turrets.Clear();

        if (Current.ProgramState != ProgramState.Playing)
        {
            return;
        }

        ScanExternalReservable(); // Handle the scanning of external reservable objects

        var maps = Find.Maps;
        foreach (var map in maps)
        {
            foreach (var def in thingDefsToLookFor)
            {
                var matchingThings = map.listerBuildings.AllBuildingsColonistOfDef(def);

                // Merge in all matching things
                buildingsToModifyPowerOn.UnionWith(matchingThings);
            }

            foreach (var def in AutodoorDefs)
            {
                var autoDoorsFound = map.listerBuildings.AllBuildingsColonistOfDef(def);

                // Merge in all matching things
                foreach (var building in autoDoorsFound)
                {
                    Autodoors.Add(building);
                }
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

            var turrets = from Building turret in map.listerBuildings.allBuildingsColonist
                where typeof(Building_Turret).IsAssignableFrom(turret.def.thingClass)
                select turret;
            foreach (var building in turrets)
            {
                Turrets.Add(building as Building_Turret);
            }

            var deepDrills = map.listerBuildings.AllBuildingsColonistOfDef(DeepDrillDef);
            DeepDrills.UnionWith(deepDrills);

            var scanners = map.listerBuildings.allBuildingsColonist.Where(building =>
                building.AllComps.Any(comp => comp.GetType().IsSubclassOf(typeof(CompScanner))));
            Scanners.UnionWith(scanners);

            var hydroponicsBasins = map.listerBuildings.AllBuildingsColonistOfDef(HydroponicsBasinDef);
            HydroponcsBasins.UnionWith(hydroponicsBasins);
        }
    }

    private void ClearVariables()
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
        DeepDrills.Clear();
        Scanners.Clear();
        HydroponcsBasins.Clear();
        Turrets.Clear();
        inUseTick = 0;
        ticksToRescan = 0;
        lastVisibleBuildings = 0;
    }

    private void EvalHydroponicsBasins()
    {
        if (selfLitHydroponicsIsLoaded)
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

    private void EvalTurrets()
    {
        foreach (var turret in Turrets)
        {
            if (turret?.Map == null)
            {
                continue;
            }

            if (turret.CurrentTarget == LocalTargetInfo.Invalid)
            {
                continue;
            }

            buildingsInUseThisTick.Add(turret);
        }
    }

    private bool IsDoorType(ThingDef def)
    {
        if (typeof(Building_Door).IsAssignableFrom(def.thingClass))
        {
            return true;
        }

        if (def.thingClass.FullName == "DoorsExpanded.Building_DoorRemote")
        {
            return true;
        }

        return false;
    }

    private void LogMessage(string Message)
    {
        if (verboseLogging is not { Value: true })
        {
            return;
        }

        Log.Message($"[TurnOnOffRePowered]: {Message}");
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
            RegisterPowerUserBuilding(defName, lowPower, highPower);
            ScheduledBuildingsDefs.Add(def);
        }
        catch (Exception e)
        {
            Logger.Error($"Error while registering a scheduled building: {e.Message}");
        }
    }

    private void UpdateDefinitions()
    {
        LogMessage("Clearing power-levels");

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
            if (highPowerMultiplier <= 0)
            {
                highPowerMultiplier = 0.001f;
            }
        }

        var doorPowerMultiplier = 10f;
        if (doorMultiplier != null)
        {
            doorPowerMultiplier = doorMultiplier.Value;
            if (doorPowerMultiplier <= 0)
            {
                doorPowerMultiplier = 0.001f;
            }
        }

        var repowerVanilla = new List<string[]>
        {
            new[] { "ElectricCrematorium", "200", "750", "Normal" },
            new[] { "ElectricSmelter", "400", "4500", "Normal" },
            new[] { "HiTechResearchBench", "100", "1000", "Normal" },
            new[] { "HydroponicsBasin", "5", "75", "Special" }

            // new string[] { "SunLamp", "0", "2900", "Special" },
            // new[] { "Autodoor", "5", "500", "Special" }
        };
        var specialCases = new List<string> { "MultiAnalyzer", "VitalsMonitor", "DeepDrill" };
        foreach (var tv in from tvDef in DefDatabase<ThingDef>.AllDefsListForReading
                 where tvDef.building?.joyKind == DefDatabase<JoyKindDef>.GetNamed("Television")
                 select tvDef)
        {
            specialCases.Add(tv.defName);
        }

        if (!applyRepowerVanilla)
        {
            repowerVanilla = new List<string[]>();
            specialCases.Add("HiTechResearchBench");
        }

        foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if ((from stringArray in repowerVanilla where stringArray[0] == def.defName select stringArray).Any())
            {
                var repowerSetting = (from stringArray in repowerVanilla
                    where stringArray[0] == def.defName
                    select stringArray).First();
                if (repowerSetting[3] == "Normal")
                {
                    RegisterPowerUserBuilding(
                        def.defName,
                        -Convert.ToInt32(repowerSetting[1]),
                        -Convert.ToInt32(repowerSetting[2]));
                }
                else
                {
                    RegisterSpecialPowerTrader(
                        def.defName,
                        -Convert.ToInt32(repowerSetting[1]),
                        -Convert.ToInt32(repowerSetting[2]));
                }

                continue;
            }

            var powerProps = def.GetCompProperties<CompProperties_Power>();
            if (powerProps == null || !typeof(CompPowerTrader).IsAssignableFrom(powerProps.compClass))
            {
                // LogMessage($"{def.defName} does not require power");
                continue;
            }

            if (powerLevels.ContainsKey(def.defName))
            {
                // LogMessage($"{def.defName} already is defined in powerlevels");
                continue;
            }

            if (specialCases.Contains(def.defName))
            {
                RegisterSpecialPowerTrader(
                    def.defName,
                    lowPower,
                    powerProps.basePowerConsumption * highPowerMultiplier * -1);
                continue;
            }

            if (!typeof(Building_WorkTable).IsAssignableFrom(def.thingClass)
                && !typeof(Building_Turret).IsAssignableFrom(def.thingClass)
                && !IsDoorType(def)
                && !def.comps.Any(comp => comp.GetType().IsSubclassOf(typeof(CompProperties_Scanner))))
            {
                //LogMessage(
                //    $"{def.defName} with thingClass {def.thingClass} is not Building_WorkTable/Building_Turret/Building_Door/Scanner");
                continue;
            }

            if (IsDoorType(def))
            {
                AutodoorDefs.Add(def);
                RegisterSpecialPowerTrader(
                    def.defName,
                    lowPower,
                    powerProps.basePowerConsumption * doorPowerMultiplier * -1);
                continue;
            }

            RegisterPowerUserBuilding(
                def.defName,
                lowPower,
                powerProps.basePowerConsumption * highPowerMultiplier * -1);
        }

        if (powerLevels.ContainsKey("FM_AIManager"))
        {
            powerLevels.Remove("FM_AIManager");
        }

        LogMessage("Initialized Components");

        medicalBedDef = ThingDef.Named("HospitalBed");
        HiTechResearchBenchDef = ThingDef.Named("HiTechResearchBench");
        DeepDrillDef = ThingDef.Named("DeepDrill");
        HydroponicsBasinDef = ThingDef.Named("HydroponicsBasin");
        ScanForThings();
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
                RegisterPowerUserBuilding(namedDef.defName, def.lowPower, def.highPower);
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
                RegisterPowerUserBuilding(namedDef.defName, def.lowPower, def.highPower);
            }

            if (def.poweredReservable)
            {
                RegisterExternalReservable(namedDef.defName, def.lowPower, def.highPower);
            }
        }
    }
}