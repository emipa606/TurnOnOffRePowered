using System.IO;
using System.Reflection;
using System.Xml.Linq;
using HarmonyLib;
using Mlie;
using UnityEngine;
using Verse;

namespace TurnOnOffRePowered;

public class TurnOnOffMod : Mod
{
    public TurnOnOffMod(ModContentPack content) : base(content)
    {
        Instance = this;
        CurrentVersion = VersionFromManifest.GetVersionFromModMetaData(content.ModMetaData);

        Settings = GetSettings<TurnOnOffSettings>();

        importOldHugsLibSettings();

        new Harmony("Mlie.TurnOnOffRePowered").PatchAll(Assembly.GetExecutingAssembly());


        // Run the old DefsLoaded logic on a long event
        LongEventHandler.QueueLongEvent(
            () =>
            {
                TurnItOnUtility.InitializeStatic();
                TurnItOnUtility.DefsLoaded(); // moved content of original DefsLoaded()
            },
            "InitializingTurnOnOff",
            false,
            null);
    }

    // Overrides
    public static string CurrentVersion { get; private set; }

    public static TurnOnOffMod Instance { get; private set; }

    public TurnOnOffSettings Settings { get; }


    private static void importOldHugsLibSettings()
    {
        var hugsLibConfig = Path.Combine(GenFilePaths.SaveDataFolderPath, "HugsLib", "ModSettings.xml");
        if (!new FileInfo(hugsLibConfig).Exists)
        {
            return;
        }

        var xml = XDocument.Load(hugsLibConfig);
        var modNodeName = "TurnOnOffRePowered";

        var modSettings = xml.Root?.Element(modNodeName);
        if (modSettings == null)
        {
            return;
        }

        foreach (var modSetting in modSettings.Elements())
        {
            if (modSetting.Name == "lowValue")
            {
                Instance.Settings.lowValue = float.Parse(modSetting.Value);
            }

            if (modSetting.Name == "highMultiplier")
            {
                Instance.Settings.highMultiplier = float.Parse(modSetting.Value);
            }

            if (modSetting.Name == "doorMultiplier")
            {
                Instance.Settings.doorMultiplier = float.Parse(modSetting.Value);
            }

            if (modSetting.Name == "applyRepowerVanilla")
            {
                Instance.Settings.applyRepowerVanilla = bool.Parse(modSetting.Value);
            }

            if (modSetting.Name == "blockUseWhenLowPower")
            {
                Instance.Settings.blockUseWhenLowPower = bool.Parse(modSetting.Value);
            }

            if (modSetting.Name == "verboseLogging")
            {
                Instance.Settings.verboseLogging = bool.Parse(modSetting.Value);
            }
        }

        Instance.Settings.Write();
        xml.Root.Element(modNodeName)?.Remove();
        xml.Save(hugsLibConfig);

        Log.Message($"[{modNodeName}]: Imported old HugLib-settings");
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        // Overrides: Delegate UI rendering to the dedicated GUI class
        TurnOnOffSettingsGUI.Draw(inRect, Settings);
    }

    public override string SettingsCategory()
    {
        return "Turn It On and Off - RePowered";
    }


    public override void WriteSettings()
    {
        TurnOnOffSettingsGUI.SavePendingChanges(Settings);

        base.WriteSettings();

        // Rebuild internal data using the updated settings
        TurnItOnUtility.ClearVariables();
        TurnItOnUtility.DefsLoaded();
    }
}