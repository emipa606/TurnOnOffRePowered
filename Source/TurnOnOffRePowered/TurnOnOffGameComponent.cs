using System;
using Verse;

namespace TurnOnOffRePowered;

// GameComponent: global Tick equivalent to old ModBase.Tick
public class TurnOnOffGameComponent : GameComponent
{
	private const int FAST_EVALUATION_INTERVAL = 4;
	private const int POWER_RECONCILIATION_INTERVAL = 60;
	private const int REGULAR_EVALUATION_INTERVAL = 15;
	private const int RESCAN_INTERVAL = 2000;
	private const int SLOW_EVALUATION_INTERVAL = 60;

	private int in_use_tick;
	private Map last_current_map;
	private int last_visible_buildings;
	private int next_fast_evaluation_tick;
	private int next_power_reconciliation_tick;
	private int next_regular_evaluation_tick;
	private int next_slow_evaluation_tick;
	private int ticks_to_rescan;

	public TurnOnOffGameComponent(Game game)
	{
	}

	public override void GameComponentTick()
	{
		try
		{
			var current_tick = Find.TickManager.TicksGame;
			if (in_use_tick == 0)
			{
				in_use_tick = current_tick;
				return;
			}

			if (in_use_tick == current_tick)
			{
				return;
			}

			in_use_tick = current_tick;
			TurnItOnUtility.BeginUsageTick();

			var current_map = Find.CurrentMap;
			if (current_map == null)
			{
				return;
			}

			if (current_map != last_current_map)
			{
				last_current_map = current_map;
				last_visible_buildings = -1;
				ticks_to_rescan = 0;
			}

			var visible_buildings = current_map.listerBuildings.allBuildingsColonist.Count;
			if (visible_buildings != last_visible_buildings)
			{
				last_visible_buildings = visible_buildings;
				ticks_to_rescan = 0;
			}

			--ticks_to_rescan;
			if (ticks_to_rescan < 0)
			{
				ticks_to_rescan = RESCAN_INTERVAL;
				TurnItOnUtility.ScanForThings();
			}

			if (current_tick >= next_fast_evaluation_tick)
			{
				next_fast_evaluation_tick = current_tick + FAST_EVALUATION_INTERVAL;
				TurnItOnUtility.RefreshFastBuildingUsage();
			}

			if (current_tick >= next_regular_evaluation_tick)
			{
				next_regular_evaluation_tick = current_tick + REGULAR_EVALUATION_INTERVAL;
				TurnItOnUtility.RefreshRegularBuildingUsage();
				TurnItOnUtility.RefreshRimfactoryBuildingUsage();
			}

			if (current_tick >= next_slow_evaluation_tick)
			{
				next_slow_evaluation_tick = current_tick + SLOW_EVALUATION_INTERVAL;
				TurnItOnUtility.RefreshSlowBuildingUsage();
			}

			TurnItOnUtility.MergePolledBuildingUsage();
			TurnItOnUtility.ApplyPowerStateChanges();

			if (current_tick >= next_power_reconciliation_tick)
			{
				next_power_reconciliation_tick = current_tick + POWER_RECONCILIATION_INTERVAL;
				TurnItOnUtility.ReconcilePowerStates();
			}
		}
		catch (Exception exception)
		{
			Log.Error($"[TurnOnOffRePowered] Exception in GameComponentTick: {exception}");
		}
	}
}
