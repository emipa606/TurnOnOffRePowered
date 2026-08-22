using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace TurnOnOffRePowered;

public static class TurnOnOffSettingsGUI
{
    private static SettingsTab currentTab = SettingsTab.General;
    private static Vector2 scrollPosition = Vector2.zero;
    private static bool settingsChanged;

    private static string searchText = "";
    private static string searchBuffer = "";

    private static List<DeviceRow> deviceRows = [];

    public static void Draw(Rect inRect, TurnOnOffSettings settings)
    {
        var ls = new Listing_Standard();
        ls.Begin(inRect);

        var rect3 = new Rect(inRect);
        var tabs = new List<TabRecord>
        {
            new("tabs.general.label".Translate(), delegate { OnTabSwitch(SettingsTab.General, settings); },
                currentTab == SettingsTab.General),
            new("tabs.devices.label".Translate(), delegate { OnTabSwitch(SettingsTab.Devices, settings); },
                currentTab == SettingsTab.Devices)
        };
        TabDrawer.DrawTabs(rect3, tabs);

        var contentRect = rect3.ContractedBy(18f);

        switch (currentTab)
        {
            case SettingsTab.General:
                DrawGeneralSettings(contentRect, settings);
                break;
            case SettingsTab.Devices:
                DrawDevicesTab(contentRect);
                break;
        }

        ls.End();
    }

    public static void SavePendingChanges(TurnOnOffSettings settings)
    {
        if (currentTab == SettingsTab.Devices)
        {
            SaveDeviceOverrides(settings);
        }
    }

    private static void OnTabSwitch(SettingsTab tab, TurnOnOffSettings settings)
    {
        switch (currentTab)
        {
            case SettingsTab.General when tab == SettingsTab.Devices:
            {
                if (settingsChanged)
                {
                    TurnItOnUtility.ClearVariables();
                    TurnItOnUtility.DefsLoaded();
                    settingsChanged = false;
                }

                BuildDeviceRows(settings);
                break;
            }
            case SettingsTab.Devices when tab == SettingsTab.General:
                SaveDeviceOverrides(settings);
                break;
        }

        currentTab = tab;
    }

    private static void BuildDeviceRows(TurnOnOffSettings settings)
    {
        deviceRows.Clear();
        var powerLevels = TurnItOnUtility.PowerLevels;
        if (powerLevels == null)
        {
            return;
        }

        foreach (var keyValuePair in powerLevels)
        {
            var defName = keyValuePair.Key;
            var basePower = keyValuePair.Value; // Base calculated value

            var isOverridden = settings.customPowerValues != null && settings.customPowerValues.ContainsKey(defName);

            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (settings.customPowerValues == null)
            {
                continue;
            }

            var row = new DeviceRow
            {
                def = def,
                defName = defName,
                idle = isOverridden ? -settings.customPowerValues[defName].x : -basePower.x,
                active = isOverridden ? -settings.customPowerValues[defName].y : -basePower.y,
                bufferIdle = (isOverridden ? -settings.customPowerValues[defName].x : -basePower.x).ToString("F0"),
                bufferActive = (isOverridden ? -settings.customPowerValues[defName].y : -basePower.y).ToString("F0")
            };
            deviceRows.Add(row);
        }

        deviceRows = deviceRows.OrderBy(r => r.def != null ? r.def.LabelCap.ToString() : r.defName).ToList();
    }

    private static void SaveDeviceOverrides(TurnOnOffSettings settings)
    {
        var newOverrides = new Dictionary<string, Vector2>();
        foreach (var row in deviceRows.Where(row =>
                     !row.baseValues.y.Equals(-row.active) || !row.baseValues.x.Equals(-row.idle)))
        {
            newOverrides[row.defName] = new Vector2(-row.idle, -row.active);
        }

        settings.customPowerValues = newOverrides;

        TurnItOnUtility.rebuildPowerBuildingCache();
    }

    private static void DrawGeneralSettings(Rect rect, TurnOnOffSettings settings)
    {
        var ls = new Listing_Standard();
        ls.Begin(rect);
        ls.Gap();

        var oldLow = settings.lowValue;
        var oldHigh = settings.highMultiplier;
        var oldDoor = settings.doorMultiplier;
        var oldRepower = settings.applyRepowerVanilla;
        var oldBlock = settings.blockUseWhenLowPower;
        var oldVerbose = settings.verboseLogging;

        ls.Label("lowValue.label".Translate() + ": " + settings.lowValue.ToString("F0"));
        settings.lowValue = ls.Slider(settings.lowValue, 1f, 100f);

        ls.Label("highMultiplier.label".Translate() + ": " + settings.highMultiplier.ToString("F2"));
        settings.highMultiplier = ls.Slider(settings.highMultiplier, 0.1f, 10f);

        ls.Label("doorMultiplier.label".Translate() + ": " + settings.doorMultiplier.ToString("F2"));
        settings.doorMultiplier = ls.Slider(settings.doorMultiplier, 0.1f, 10f);

        ls.CheckboxLabeled("applyRepowerVanilla.label".Translate(), ref settings.applyRepowerVanilla);
        ls.CheckboxLabeled("blockUseWhenLowPower.label".Translate(), ref settings.blockUseWhenLowPower);
        ls.CheckboxLabeled("verboseLogging.label".Translate(), ref settings.verboseLogging);

        if (!oldLow.Equals(settings.lowValue) || !oldHigh.Equals(settings.highMultiplier) ||
            !oldDoor.Equals(settings.doorMultiplier) || oldRepower != settings.applyRepowerVanilla ||
            oldBlock != settings.blockUseWhenLowPower || oldVerbose != settings.verboseLogging)
        {
            settingsChanged = true;
        }

        ls.Gap();
        if (ls.ButtonText("resetdefaults.label".Translate()))
        {
            settings.SetToDefaults();
            settingsChanged = false;
            TurnItOnUtility.ClearVariables();
            TurnItOnUtility.DefsLoaded();
        }

        if (TurnOnOffMod.CurrentVersion != null)
        {
            ls.Gap();
            GUI.contentColor = Color.gray;
            ls.Label("currentModVersion.label".Translate(TurnOnOffMod.CurrentVersion));
            GUI.contentColor = Color.white;
        }

        ls.End();
    }

    private static void DrawDevicesTab(Rect rect)
    {
        var searchBarHeight = 30f;
        var searchRect = new Rect(rect.x, rect.y, rect.width, searchBarHeight);
        var textFieldRect = new Rect(searchRect.x, searchRect.y, searchRect.width - 30f, searchRect.height);
        var clearButtonRect = new Rect(searchRect.x + searchRect.width - 28f, searchRect.y + 2f, 24f, 24f);

        var newBuffer = Widgets.TextField(textFieldRect, searchBuffer);
        TooltipHandler.TipRegion(textFieldRect, "search.tooltip".Translate());
        if (newBuffer != searchBuffer)
        {
            searchBuffer = newBuffer;
            searchText = searchBuffer;
        }

        if (Widgets.ButtonText(clearButtonRect, "X"))
        {
            searchBuffer = "";
            searchText = "";
            GUI.FocusControl(null);
        }

        IEnumerable<DeviceRow> filteredRows = deviceRows;
        if (!string.IsNullOrEmpty(searchText))
        {
            filteredRows = deviceRows.Where(row =>
                row.searchString.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        if (!filteredRows.Any())
        {
            var labelRect = new Rect(rect.x, rect.y + searchBarHeight + 20f, rect.width, 30f);
            Widgets.Label(labelRect, "nodevicesdata.label".Translate());
            return;
        }

        var rowHeight = 52f;
        var headerHeight = 30f;
        var filteredRowsList = filteredRows.ToList();
        var totalHeight = headerHeight + (filteredRowsList.Count * rowHeight) + 20f;

        var tableRect = new Rect(rect.x, rect.y + searchBarHeight + 8f, rect.width,
            rect.height - searchBarHeight - 18f);
        var scrollRect = new Rect(0f, 0f, tableRect.width - 20f, tableRect.height);
        var viewRect = new Rect(0f, 0f, scrollRect.width - 16f, totalHeight);

        try
        {
            Widgets.BeginScrollView(tableRect, ref scrollPosition, viewRect);
            var curY = 0f;
            var col1 = 0.55f;
            var col2 = 0.20f;
            var col3 = 0.20f;

            var headerRect = new Rect(0f, curY, viewRect.width, headerHeight);
            Widgets.DrawBoxSolid(headerRect, new Color(0.2f, 0.2f, 0.2f));
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(0f, curY, viewRect.width * col1, headerHeight), "devicename.label".Translate());
            Widgets.Label(new Rect(viewRect.width * col1, curY, viewRect.width * col2, headerHeight),
                "deviceidle.label".Translate());
            Widgets.Label(new Rect(viewRect.width * (col1 + col2), curY, viewRect.width * col3, headerHeight),
                "deviceactive.label".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            curY += headerHeight;

            var rowIndex = 0;
            foreach (var row in filteredRowsList)
            {
                var rowRect = new Rect(0f, curY, viewRect.width, rowHeight);
                if (rowIndex % 2 == 0)
                {
                    Widgets.DrawBoxSolid(rowRect, new Color(0.15f, 0.15f, 0.15f));
                }

                var x = 0f;
                var label = row.def != null ? row.def.LabelCap.ToString() : row.defName;
                var nameRect = new Rect(x, curY, viewRect.width * col1, rowHeight);
                Widgets.Label(nameRect.LeftPart(0.8f), label);
                if (!string.IsNullOrEmpty(row.description))
                {
                    TooltipHandler.TipRegion(nameRect, row.description);
                }

                if (row.def != null)
                {
                    Widgets.ThingIcon(nameRect.RightPart(0.2f), row.def);
                }

                x += viewRect.width * col1;

                Widgets.TextFieldNumeric(new Rect(x, curY, viewRect.width * col2, rowHeight), ref row.idle,
                    ref row.bufferIdle);

                x += viewRect.width * col2;

                Widgets.TextFieldNumeric(new Rect(x, curY, viewRect.width * col3, rowHeight), ref row.active,
                    ref row.bufferActive);

                x += viewRect.width * col3;

                var resetRect = new Rect(new Vector2(x + 4f, curY + ((rowHeight - 24f) / 2f)), new Vector2(24f, 24f));
                var baseValues = row.baseValues;
                var hasChanges = !baseValues.y.Equals(-row.active) || !baseValues.x.Equals(-row.idle);
                if (hasChanges &&
                    Widgets.ButtonImage(resetRect, TexUI.RotLeftTex, tooltip: "reset.label".Translate()) &&
                    TurnItOnUtility.PowerLevels.TryGetValue(row.defName, out var basePower))
                {
                    row.idle = -basePower.x;
                    row.active = -basePower.y;
                    row.bufferIdle = row.idle.ToString("F0");
                    row.bufferActive = row.active.ToString("F0");
                }

                curY += rowHeight;
                rowIndex++;
            }
        }
        finally
        {
            Widgets.EndScrollView();
        }
    }

    private enum SettingsTab
    {
        General,
        Devices
    }

    private sealed class DeviceRow
    {
        public float active;
        public string bufferActive;
        public string bufferIdle;
        public ThingDef def;
        public string defName;
        public float idle;

        public Vector2 baseValues
        {
            get
            {
                if (field != Vector2.zero)
                {
                    return field;
                }

                field = TurnItOnUtility.PowerLevels.TryGetValue(defName, out var basePower)
                    ? new Vector2(basePower.x, basePower.y)
                    : Vector2.zero;

                return field;
            }
        }

        public string description
        {
            get
            {
                if (field != null)
                {
                    return field;
                }

                field = def?.description ?? "";

                return field;
            }
        }


        public string searchString
        {
            get
            {
                if (field != null)
                {
                    return field;
                }

                field = defName;
                if (def == null)
                {
                    return field;
                }

                field += $" {def.LabelCap}";
                if (def.modContentPack != null)
                {
                    field += $" {def.modContentPack.Name}";
                }

                return field;
            }
        }
    }
}