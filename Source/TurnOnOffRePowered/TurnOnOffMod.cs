using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using Mlie;
using RimWorld;
using UnityEngine;
using Verse;

namespace TurnOnOffRePowered
{
    public class TurnOnOffMod : Mod
    {
        private static string currentVersion;

        // MODIFICATION: tab management
        private enum SettingsTab
        {
            General, Devices
        }
        private SettingsTab currentTab = SettingsTab.General;
        private Vector2 scrollPosition = Vector2.zero;
        private bool settingsChanged = false;

        // MODIFICATION: search and filter for Devices tab
        private string searchText = "";
        private string searchBuffer = "";

        // MODIFICATION: in-memory table for Devices tab
        private List<DeviceRow> deviceRows = new List<DeviceRow>();
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

        public TurnOnOffMod(ModContentPack content) : base(content)
        {
            Instance = this;
            currentVersion = VersionFromManifest.GetVersionFromModMetaData(content.ModMetaData);

            Settings = GetSettings<TurnOnOffSettings>();

            importOldHugsLibSettings();

            new Harmony("Mlie.TurnOnOffRePowered").PatchAll(Assembly.GetExecutingAssembly());

            LongEventHandler.QueueLongEvent(
                () =>
                {
                    TurnItOnUtility.InitializeStatic();
                    TurnItOnUtility.DefsLoaded();
                },
                "InitializingTurnOnOff",
                false,
                null);
        }

        public static TurnOnOffMod Instance
        {
            get; private set;
        }
        public TurnOnOffSettings Settings
        {
            get;
        }

        private static void importOldHugsLibSettings()
        {
            var hugsLibConfig = Path.Combine(GenFilePaths.SaveDataFolderPath, "HugsLib", "ModSettings.xml");
            if (!new FileInfo(hugsLibConfig).Exists)
                return;

            var xml = XDocument.Load(hugsLibConfig);
            var modNodeName = "TurnOnOffRePowered";
            var modSettings = xml.Root?.Element(modNodeName);
            if (modSettings == null)
                return;

            foreach (var modSetting in modSettings.Elements())
            {
                switch (modSetting.Name.LocalName)
                {
                    case "lowValue":
                        Instance.Settings.lowValue = float.Parse(modSetting.Value);
                        break;
                    case "highMultiplier":
                        Instance.Settings.highMultiplier = float.Parse(modSetting.Value);
                        break;
                    case "doorMultiplier":
                        Instance.Settings.doorMultiplier = float.Parse(modSetting.Value);
                        break;
                    case "applyRepowerVanilla":
                        Instance.Settings.applyRepowerVanilla = bool.Parse(modSetting.Value);
                        break;
                    case "blockUseWhenLowPower":
                        Instance.Settings.blockUseWhenLowPower = bool.Parse(modSetting.Value);
                        break;
                    case "verboseLogging":
                        Instance.Settings.verboseLogging = bool.Parse(modSetting.Value);
                        break;
                }
            }

            Instance.Settings.Write();
            xml.Root.Element(modNodeName)?.Remove();
            xml.Save(hugsLibConfig);

            Log.Message($"[{modNodeName}]: Imported old HugLib-settings");
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            Rect rect3 = new Rect(inRect);

            // MODIFICATION: draw tabs
            var tabs = new List<TabRecord>
            {
                new TabRecord("TabGeneral".Translate(), delegate { OnTabSwitch(SettingsTab.General); }, currentTab == SettingsTab.General),
                new TabRecord("TabDevices".Translate(), delegate { OnTabSwitch(SettingsTab.Devices); }, currentTab == SettingsTab.Devices)
            };
            TabDrawer.DrawTabs(rect3, tabs);

            Rect contentRect = rect3.ContractedBy(18f);

            switch (currentTab)
            {
                case SettingsTab.General:
                    DrawGeneralSettings(contentRect);
                    break;
                case SettingsTab.Devices:
                    DrawDevicesTab(contentRect);
                    break;
            }

            listingStandard.End();
        }

        // MODIFICATION: tab switching logic
        private void OnTabSwitch(SettingsTab tab)
        {
            if (currentTab == SettingsTab.General && tab == SettingsTab.Devices)
            {
                if (settingsChanged)
                {
                    TurnItOnUtility.RebuildPowerLevelsNoScan();
                    settingsChanged = false;
                }
                BuildDeviceRows();
            }
            else if (currentTab == SettingsTab.Devices && tab == SettingsTab.General)
            {
                SaveDeviceOverrides();
            }
            currentTab = tab;
        }

        // MODIFICATION: build deviceRows from powerLevels and customPowerValues
        private void BuildDeviceRows()
        {
            deviceRows.Clear();
            var powerLevels = TurnItOnUtility.powerLevels;
            if (powerLevels == null)
                return;
            foreach (var kv in powerLevels)
            {
                string defName = kv.Key;
                Vector2 power = kv.Value;
                bool isOverridden = Settings.customPowerValues.ContainsKey(defName);
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
                var row = new DeviceRow
                {
                    def = def,
                    defName = defName,
                    idle = -power.x,
                    active = -power.y,
                    isOverridden = isOverridden,
                    bufferIdle = (-power.x).ToString("F0"),
                    bufferActive = (-power.y).ToString("F0")
                };
                deviceRows.Add(row);
            }
            // Sort by localized label (convert TaggedString to string safely)
            deviceRows = deviceRows.OrderBy(r => r.def != null ? r.def.LabelCap.ToString() : r.defName).ToList();
        }

        // MODIFICATION: save deviceRows overrides back to customPowerValues
        private void SaveDeviceOverrides()
        {
            var newOverrides = new Dictionary<string, Vector2>();
            foreach (var row in deviceRows)
            {
                if (row.isOverridden)
                {
                    newOverrides[row.defName] = new Vector2(-row.idle, -row.active);
                }
            }
            Settings.customPowerValues = newOverrides;
            settingsChanged = true;
        }

        private void DrawGeneralSettings(Rect rect)
        {
            var ls = new Listing_Standard();
            ls.Begin(rect);
            ls.Gap();

            // MODIFICATION: store current values to detect changes
            float oldLow = Settings.lowValue;
            float oldHigh = Settings.highMultiplier;
            float oldDoor = Settings.doorMultiplier;
            bool oldRepower = Settings.applyRepowerVanilla;
            bool oldBlock = Settings.blockUseWhenLowPower;
            bool oldVerbose = Settings.verboseLogging;

            ls.Label("lowValue.label".Translate() + ": " + Settings.lowValue.ToString("F0"));
            Settings.lowValue = ls.Slider(Settings.lowValue, 1f, 100f);

            ls.Label("highMultiplier.label".Translate() + ": " + Settings.highMultiplier.ToString("F2"));
            Settings.highMultiplier = ls.Slider(Settings.highMultiplier, 0.1f, 10f);

            ls.Label("doorMultiplier.label".Translate() + ": " + Settings.doorMultiplier.ToString("F2"));
            Settings.doorMultiplier = ls.Slider(Settings.doorMultiplier, 0.1f, 10f);

            ls.CheckboxLabeled("applyRepowerVanilla.label".Translate(), ref Settings.applyRepowerVanilla);
            ls.CheckboxLabeled("blockUseWhenLowPower.label".Translate(), ref Settings.blockUseWhenLowPower);
            ls.CheckboxLabeled("verboseLogging.label".Translate(), ref Settings.verboseLogging);

            // MODIFICATION: detect changes and set flag
            if (oldLow != Settings.lowValue || oldHigh != Settings.highMultiplier ||
                oldDoor != Settings.doorMultiplier || oldRepower != Settings.applyRepowerVanilla ||
                oldBlock != Settings.blockUseWhenLowPower || oldVerbose != Settings.verboseLogging)
            {
                settingsChanged = true;
            }

            ls.Gap();
            if (ls.ButtonText("Reset to defaults"))
            {
                Settings.SetToDefaults();
                // MODIFICATION: clear overrides and reset flag
                settingsChanged = false;
                TurnItOnUtility.ClearVariables();
                TurnItOnUtility.DefsLoaded();
            }

            if (currentVersion != null)
            {
                ls.Gap();
                GUI.contentColor = Color.gray;
                ls.Label("currentModVersion.label".Translate(currentVersion));
                GUI.contentColor = Color.white;
            }

            ls.End();
        }

        // MODIFICATION: Devices tab with editable table, search and filter
        private void DrawDevicesTab(Rect rect)
        {
            // --- Search bar ---
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

            // Clear button (X) – using simple text button instead of image
            if (Widgets.ButtonText(clearButtonRect, "X"))
            {
                searchBuffer = "";
                searchText = "";
                GUI.FocusControl(null);
            }

            // --- Apply filter ---
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

            // If no rows after filter, show message
            if (!filteredRows.Any())
            {
                Rect labelRect = new Rect(rect.x, rect.y + searchBarHeight + 20f, rect.width, 30f);
                Widgets.Label(labelRect, "NoDevicesData".Translate());
                return;
            }

            // --- Table rendering ---
            float rowHeight = 28f;
            float headerHeight = 30f;
            // Use filteredRows list to avoid repeated ToList() calls
            List<DeviceRow> filteredRowsList = filteredRows.ToList();
            float totalHeight = headerHeight + filteredRowsList.Count * rowHeight + 20f;

            Rect tableRect = new Rect(rect.x, rect.y + searchBarHeight + 8f, rect.width, rect.height - searchBarHeight - 8f);
            var scrollRect = new Rect(0f, 0f, tableRect.width - 20f, tableRect.height);
            var viewRect = new Rect(0f, 0f, scrollRect.width - 16f, totalHeight);

            try
            {
                Widgets.BeginScrollView(tableRect, ref scrollPosition, viewRect);

                float curY = 0f;
                float col1 = 0.30f; // Device name
                float col2 = 0.25f; // Idle
                float col3 = 0.25f; // Active
                float col4 = 0.20f; // Override checkbox

                // Headers
                Rect headerRect = new Rect(0f, curY, viewRect.width, headerHeight);
                Widgets.DrawBoxSolid(headerRect, new Color(0.2f, 0.2f, 0.2f));
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(0f, curY, viewRect.width * col1, headerHeight), "DeviceName".Translate());
                Widgets.Label(new Rect(viewRect.width * col1, curY, viewRect.width * col2, headerHeight), "DeviceIdle".Translate());
                Widgets.Label(new Rect(viewRect.width * (col1 + col2), curY, viewRect.width * col3, headerHeight), "DeviceActive".Translate());
                Widgets.Label(new Rect(viewRect.width * (col1 + col2 + col3), curY, viewRect.width * col4, headerHeight), "Override".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                curY += headerHeight;

                // Rows – use index counter instead of filteredRowsList.IndexOf(row)
                int rowIndex = 0;
                foreach (var row in filteredRowsList)
                {
                    Rect rowRect = new Rect(0f, curY, viewRect.width, rowHeight);
                    if (rowIndex % 2 == 0)
                        Widgets.DrawBoxSolid(rowRect, new Color(0.15f, 0.15f, 0.15f));

                    float x = 0f;
                    // Name
                    string label = row.def != null ? row.def.LabelCap.ToString() : row.defName;
                    Rect nameRect = new Rect(x, curY, viewRect.width * col1, rowHeight);
                    Widgets.Label(nameRect, label);
                    if (row.def != null && !string.IsNullOrEmpty(row.def.description))
                    {
                        TooltipHandler.TipRegion(nameRect, row.def.description);
                    }
                    x += viewRect.width * col1;

                    // Idle – using TextFieldNumeric
                    float oldIdle = row.idle;
                    Widgets.TextFieldNumeric(new Rect(x, curY, viewRect.width * col2, rowHeight), ref row.idle, ref row.bufferIdle, 0f, 1e9f);
                    if (row.idle != oldIdle)
                    {
                        row.isOverridden = true;
                    }
                    x += viewRect.width * col2;

                    // Active – using TextFieldNumeric
                    float oldActive = row.active;
                    Widgets.TextFieldNumeric(new Rect(x, curY, viewRect.width * col3, rowHeight), ref row.active, ref row.bufferActive, 0f, 1e9f);
                    if (row.active != oldActive)
                    {
                        row.isOverridden = true;
                    }
                    x += viewRect.width * col3;

                    // Override checkbox
                    bool newOverride = row.isOverridden;
                    Vector2 checkPos = new Vector2(x + 4f, curY + (rowHeight - 24f) / 2f);
                    Widgets.Checkbox(checkPos, ref newOverride);
                    if (newOverride != row.isOverridden)
                    {
                        row.isOverridden = newOverride;
                        if (!row.isOverridden)
                        {
                            if (row.def != null)
                            {
                                Vector2 computed = TurnItOnUtility.CalculatePowerForDef(row.def, Settings);
                                if (computed != Vector2.zero)
                                {
                                    row.idle = -computed.x;
                                    row.active = -computed.y;
                                    row.bufferIdle = row.idle.ToString("F0");
                                    row.bufferActive = row.active.ToString("F0");
                                }
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

        public override string SettingsCategory()
        {
            return "Turn It On and Off - RePowered";
        }

        public override void WriteSettings()
        {
            // MODIFICATION: ensure any unsaved changes from Devices tab are saved before writing
            if (currentTab == SettingsTab.Devices)
                SaveDeviceOverrides();

            base.WriteSettings();
            TurnItOnUtility.ClearVariables();
            TurnItOnUtility.DefsLoaded();
        }
    }
}