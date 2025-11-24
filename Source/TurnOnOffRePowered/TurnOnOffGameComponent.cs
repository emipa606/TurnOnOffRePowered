using RimWorld;
using System;
using Verse;

namespace TurnOnOffRePowered;

// GameComponent: global Tick equivalent to old ModBase.Tick
public class TurnOnOffGameComponent : GameComponent
{
    private int inUseTick;
    private int lastVisibleBuildings;
    private int ticksToRescan;

    public TurnOnOffGameComponent(Game game) : base()
    {
        //TurnItOnUtility.LogMessage("TurnOnOffGameComponent ctor");
        // nothing heavy here; real initialization runs in DefsLoaded/InitializeStatic
    }

    public override void GameComponentTick()
    {
        try
        {
            int currentTick = Find.TickManager.TicksGame;
            TurnItOnUtility.EvaluateRimfactoryWork(); // optional smaller calls

            if(inUseTick == 0)
            {
                inUseTick = currentTick;
                return;
            }

            if(inUseTick != currentTick)
            {
                inUseTick = currentTick;

                TurnItOnUtility.buildingsThatWereUsedLastTick.Clear();
                TurnItOnUtility.buildingsThatWereUsedLastTick.UnionWith(TurnItOnUtility.buildingsInUseThisTick);
                TurnItOnUtility.buildingsInUseThisTick.Clear();
            }

            // Only run scanning/evaluation when playing
            if(Find.CurrentMap == null)
            {
                return;
            }

            var visibleBuildings = Find.CurrentMap.listerBuildings.allBuildingsColonist.Count;
            if(visibleBuildings != lastVisibleBuildings)
            {
                lastVisibleBuildings = visibleBuildings;
                ticksToRescan = 0;
            }

            --ticksToRescan;
            if(ticksToRescan < 0)
            {
                ticksToRescan = 2000;
                TurnItOnUtility.ScanForThings();
            }

            TurnItOnUtility.EvaluateBeds();
            TurnItOnUtility.EvaluateResearchTables();
            TurnItOnUtility.EvaluateAutodoors();
            TurnItOnUtility.EvaluateDeepDrills();
            TurnItOnUtility.EvaluateScanners();
            TurnItOnUtility.EvalHydroponicsBasins();
            TurnItOnUtility.EvalTurrets();
            TurnItOnUtility.EvalScheduledBuildings();

            // Apply the collected power-level changes
            foreach(var thing in TurnItOnUtility.buildingsToModifyPowerOn)
            {
                if(thing == null)
                {
                    TurnItOnUtility.LogMessage("Tried to modify power level for thing which no longer exists");
                    continue;
                }

                var powerComp = thing.TryGetComp<CompPowerTrader>();
                if(powerComp != null && TurnItOnUtility.powerLevels.ContainsKey(thing.def.defName))
                {
                    powerComp.PowerOutput = TurnItOnUtility.powerLevels[thing.def.defName][0];
                }
            }

            foreach(var building in TurnItOnUtility.buildingsThatWereUsedLastTick)
            {
                if(!TurnItOnUtility.buildingsToModifyPowerOn.Contains(building))
                {
                    continue;
                }

                var powerComp = building.TryGetComp<CompPowerTrader>();
                if(powerComp != null && TurnItOnUtility.powerLevels.ContainsKey(building.def.defName))
                {
                    powerComp.PowerOutput = TurnItOnUtility.powerLevels[building.def.defName][1];
                }
            }
        } catch(Exception e)
        {
            Log.Error($"[TurnOnOffRePowered] Exception in GameComponentTick: {e}");
        }
    }
}
