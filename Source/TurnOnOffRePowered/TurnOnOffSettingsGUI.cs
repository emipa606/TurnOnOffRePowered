using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace TurnOnOffRePowered;

public static class TurnOnOffSettingsGUI
{
    private enum SettingsTab
    {
        General, Devices
    }

    private static SettingsTab currentTab = SettingsTab.General;
    private static Vector2 scrollPosition = Vector2.zero;
    private static bool settingsChanged = false;

    private static string searchText = "";
    private static string searchBuffer = "";

    private static List<DeviceRow> deviceRows = new List<DeviceRow>();

    private class DeviceRow
    {
        public ThingDef def;
        public string defName;
        public float idle;
        public float active;
        public bool isOverridden;
        public string bufferIdle;
        public string bufferActive;
    }

    public static void Draw(Rect inRect, TurnOnOffSettings settings)
    {
        var ls = new Listing_Standard();
        ls.Begin(inRect);

        var rect3 = new Rect(inRect);
        var tabs = new List<TabRecord>
        {
            new TabRecord("TabGeneral".Translate(), delegate { OnTabSwitch(SettingsTab.General, settings); }, currentTab == SettingsTab.General),
            new TabRecord("TabDevices".Translate(), delegate { OnTabSwitch(SettingsTab.Devices, settings); }, currentTab == SettingsTab.Devices)
        };
        TabDrawer.DrawTabs(rect3, tabs);

        Rect contentRect = rect3.ContractedBy(18f);

        switch (currentTab)
        {
            case SettingsTab.General:
                DrawGeneralSettings(contentRect, settings);
                break;
            case SettingsTab.Devices:
                DrawDevicesTab(contentRect, settings);
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
        if (currentTab == SettingsTab.General && tab == SettingsTab.Devices)
        {
            if (settingsChanged)
            {
                TurnItOnUtility.ClearVariables();
                TurnItOnUtility.DefsLoaded();
                settingsChanged = false;
            }
            BuildDeviceRows(settings);
        }
        else if (currentTab == SettingsTab.Devices && tab == SettingsTab.General)
        {
            SaveDeviceOverrides(settings);
        }
        currentTab = tab;
    }

    private static void BuildDeviceRows(TurnOnOffSettings settings)
    {
        deviceRows.Clear();
        var powerLevels = TurnItOnUtility.powerLevels;
        if (powerLevels == null)
            return;

        foreach (var kv in powerLevels)
        {
            string defName = kv.Key;
            Vector2 basePower = kv.Value; // Base calculated value

            bool isOverridden = settings.customPowerValues != null && settings.customPowerValues.ContainsKey(defName);

            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            var row = new DeviceRow
            {
                def = def,
                defName = defName,
                idle = isOverridden ? -settings.customPowerValues[defName].x : -basePower.x,
                active = isOverridden ? -settings.customPowerValues[defName].y : -basePower.y,
                isOverridden = isOverridden,
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
        foreach (var row in deviceRows)
        {
            if (row.isOverridden)
            {
                newOverrides[row.defName] = new Vector2(-row.idle, -row.active);
            }
        }
        settings.customPowerValues = newOverrides;

        TurnItOnUtility.rebuildPowerBuildingCache();
    }

    private static void DrawGeneralSettings(Rect rect, TurnOnOffSettings settings)
    {
        var ls = new Listing_Standard();
        ls.Begin(rect);
        ls.Gap();

        float oldLow = settings.lowValue;
        float oldHigh = settings.highMultiplier;
        float oldDoor = settings.doorMultiplier;
        bool oldRepower = settings.applyRepowerVanilla;
        bool oldBlock = settings.blockUseWhenLowPower;
        bool oldVerbose = settings.verboseLogging;

        ls.Label("lowValue.label".Translate() + ": " + settings.lowValue.ToString("F0"));
        settings.lowValue = ls.Slider(settings.lowValue, 1f, 100f);

        ls.Label("highMultiplier.label".Translate() + ": " + settings.highMultiplier.ToString("F2"));
        settings.highMultiplier = ls.Slider(settings.highMultiplier, 0.1f, 10f);

        ls.Label("doorMultiplier.label".Translate() + ": " + settings.doorMultiplier.ToString("F2"));
        settings.doorMultiplier = ls.Slider(settings.doorMultiplier, 0.1f, 10f);

        ls.CheckboxLabeled("applyRepowerVanilla.label".Translate(), ref settings.applyRepowerVanilla);
        ls.CheckboxLabeled("blockUseWhenLowPower.label".Translate(), ref settings.blockUseWhenLowPower);
        ls.CheckboxLabeled("verboseLogging.label".Translate(), ref settings.verboseLogging);

        if (oldLow != settings.lowValue || oldHigh != settings.highMultiplier ||
            oldDoor != settings.doorMultiplier || oldRepower != settings.applyRepowerVanilla ||
            oldBlock != settings.blockUseWhenLowPower || oldVerbose != settings.verboseLogging)
        {
            settingsChanged = true;
        }

        ls.Gap();
        if (ls.ButtonText("Reset to defaults"))
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

    private static void DrawDevicesTab(Rect rect, TurnOnOffSettings settings)
    {
        float searchBarHeight = 30f;
        Rect searchRect = new Rect(rect.x, rect.y, rect.width, searchBarHeight);
        Rect textFieldRect = new Rect(searchRect.x, searchRect.y, searchRect.width - 30f, searchRect.height);
        Rect clearButtonRect = new Rect(searchRect.x + searchRect.width - 28f, searchRect.y + 2f, 24f, 24f);

        string newBuffer = Widgets.TextField(textFieldRect, searchBuffer);
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
            {
                string label = row.def != null ? row.def.LabelCap.ToString() : row.defName;
                return label.IndexOf(searchText, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                       row.defName.IndexOf(searchText, System.StringComparison.OrdinalIgnoreCase) >= 0;
            });
        }

        if (!filteredRows.Any())
        {
            Rect labelRect = new Rect(rect.x, rect.y + searchBarHeight + 20f, rect.width, 30f);
            Widgets.Label(labelRect, "NoDevicesData".Translate());
            return;
        }

        float rowHeight = 28f;
        float headerHeight = 30f;
        List<DeviceRow> filteredRowsList = filteredRows.ToList();
        float totalHeight = headerHeight + filteredRowsList.Count * rowHeight + 20f;

        Rect tableRect = new Rect(rect.x, rect.y + searchBarHeight + 8f, rect.width, rect.height - searchBarHeight - 8f);
        var scrollRect = new Rect(0f, 0f, tableRect.width - 20f, tableRect.height);
        var viewRect = new Rect(0f, 0f, scrollRect.width - 16f, totalHeight);

        try
        {
            Widgets.BeginScrollView(tableRect, ref scrollPosition, viewRect);
            float curY = 0f;
            float col1 = 0.30f;
            float col2 = 0.25f;
            float col3 = 0.25f;
            float col4 = 0.20f;

            Rect headerRect = new Rect(0f, curY, viewRect.width, headerHeight);
            Widgets.DrawBoxSolid(headerRect, new Color(0.2f, 0.2f, 0.2f));
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(0f, curY, viewRect.width * col1, headerHeight), "DeviceName".Translate());
            Widgets.Label(new Rect(viewRect.width * col1, curY, viewRect.width * col2, headerHeight), "DeviceIdle".Translate());
            Widgets.Label(new Rect(viewRect.width * (col1 + col2), curY, viewRect.width * col3, headerHeight), "DeviceActive".Translate());
            Widgets.Label(new Rect(viewRect.width * (col1 + col2 + col3), curY, viewRect.width * col4, headerHeight), "Override".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            curY += headerHeight;

            int rowIndex = 0;
            foreach (var row in filteredRowsList)
            {
                Rect rowRect = new Rect(0f, curY, viewRect.width, rowHeight);
                if (rowIndex % 2 == 0)
                    Widgets.DrawBoxSolid(rowRect, new Color(0.15f, 0.15f, 0.15f));

                float x = 0f;
                string label = row.def != null ? row.def.LabelCap.ToString() : row.defName;
                Rect nameRect = new Rect(x, curY, viewRect.width * col1, rowHeight);
                Widgets.Label(nameRect, label);
                if (row.def != null && !string.IsNullOrEmpty(row.def.description))
                {
                    TooltipHandler.TipRegion(nameRect, row.def.description);
                }
                x += viewRect.width * col1;

                float oldIdle = row.idle;
                Widgets.TextFieldNumeric(new Rect(x, curY, viewRect.width * col2, rowHeight), ref row.idle, ref row.bufferIdle, 0f, 1e9f);
                if (row.idle != oldIdle)
                    row.isOverridden = true;
                x += viewRect.width * col2;

                float oldActive = row.active;
                Widgets.TextFieldNumeric(new Rect(x, curY, viewRect.width * col3, rowHeight), ref row.active, ref row.bufferActive, 0f, 1e9f);
                if (row.active != oldActive)
                    row.isOverridden = true;
                x += viewRect.width * col3;

                bool newOverride = row.isOverridden;
                Vector2 checkPos = new Vector2(x + 4f, curY + (rowHeight - 24f) / 2f);
                Widgets.Checkbox(checkPos, ref newOverride);

                if (newOverride != row.isOverridden)
                {
                    row.isOverridden = newOverride;
                    if (!row.isOverridden)
                    {
                        if (TurnItOnUtility.powerLevels.TryGetValue(row.defName, out var basePower))
                        {
                            row.idle = -basePower.x;
                            row.active = -basePower.y;
                            row.bufferIdle = row.idle.ToString("F0");
                            row.bufferActive = row.active.ToString("F0");
                        }
                    }
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
}