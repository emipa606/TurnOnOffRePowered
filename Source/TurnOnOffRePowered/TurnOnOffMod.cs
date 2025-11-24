using HarmonyLib;
using Mlie;
using System.IO;
using System.Reflection;
using System.Xml.Linq;
using UnityEngine;
using Verse;

namespace TurnOnOffRePowered;

public class TurnOnOffMod : Mod
{
    private static string currentVersion;

    public TurnOnOffMod(ModContentPack content) : base(content)
    {
        Instance = this;
        currentVersion = VersionFromManifest.GetVersionFromModMetaData(content.ModMetaData);

        Settings = GetSettings<TurnOnOffSettings>();

        importOldHugsLibSettings();

        new Harmony("Mlie.TurnOnOffRePowered").PatchAll(Assembly.GetExecutingAssembly());


        // Run the old DefsLoaded logic on a long event
        LongEventHandler.QueueLongEvent(
            () =>
            {
                TurnItOnUtility.InitializeStatic();
                TurnItOnUtility.DefsLoaded();         // moved content of original DefsLoaded()
            },
            "InitializingTurnOnOff",
            false,
            null);
    }


    private static void importOldHugsLibSettings()
    {
        var hugsLibConfig = Path.Combine(GenFilePaths.SaveDataFolderPath, "HugsLib", "ModSettings.xml");
        if(!new FileInfo(hugsLibConfig).Exists)
        {
            return;
        }

        var xml = XDocument.Load(hugsLibConfig);
        var modNodeName = "TurnOnOffRePowered";

        var modSettings = xml.Root?.Element(modNodeName);
        if(modSettings == null)
        {
            return;
        }

        foreach(var modSetting in modSettings.Elements())
        {
            if(modSetting.Name == "lowValue")
            {
                Instance.Settings.lowValue = float.Parse(modSetting.Value);
            }
            if(modSetting.Name == "highMultiplier")
            {
                Instance.Settings.highMultiplier = float.Parse(modSetting.Value);
            }
            if(modSetting.Name == "doorMultiplier")
            {
                Instance.Settings.doorMultiplier = int.Parse(modSetting.Value);
            }
            if(modSetting.Name == "applyRepowerVanilla")
            {
                Instance.Settings.applyRepowerVanilla = bool.Parse(modSetting.Value);
            }
            if(modSetting.Name == "blockUseWhenLowPower")
            {
                Instance.Settings.blockUseWhenLowPower = bool.Parse(modSetting.Value);
            }
            if(modSetting.Name == "verboseLogging")
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
        Listing_Standard ls = new Listing_Standard();
        ls.Begin(inRect);
        ls.Gap();

        ls.Label("lowValue.label".Translate() + ": " + Settings.lowValue.ToString("F0"));
        Settings.lowValue = ls.Slider(Settings.lowValue, 1f, 100f);

        ls.Label("highMultiplier.label".Translate() + ": " + Settings.highMultiplier.ToString("F2"));
        Settings.highMultiplier = ls.Slider(Settings.highMultiplier, 0.1f, 10f);

        ls.Label("doorMultiplier.label".Translate() + ": " + Settings.doorMultiplier.ToString("F2"));
        Settings.doorMultiplier = ls.Slider(Settings.doorMultiplier, 0.1f, 10f);

        ls.CheckboxLabeled("applyRepowerVanilla.label".Translate(), ref Settings.applyRepowerVanilla);
        ls.CheckboxLabeled("blockUseWhenLowPower.label".Translate(), ref Settings.blockUseWhenLowPower);
        ls.CheckboxLabeled("verboseLogging.label".Translate(), ref Settings.verboseLogging);

        ls.Gap();
        if(ls.ButtonText("Reset to defaults"))
        {
            Settings.SetToDefaults();
            TurnItOnUtility.ClearVariables();
            TurnItOnUtility.DefsLoaded();
        }
        if(currentVersion != null)
        {
            ls.Gap();
            GUI.contentColor = Color.gray;
            ls.Label("currentModVersion.label".Translate(currentVersion));
            GUI.contentColor = Color.white;
        }
        ls.End();
    }

    public override string SettingsCategory() { return "Turn It On and Off - RePowered"; }


    public override void WriteSettings()
    {
        base.WriteSettings();

        // Rebuild internal data using the updated settings
        TurnItOnUtility.ClearVariables();
        TurnItOnUtility.DefsLoaded();
    }

    public static TurnOnOffMod Instance { get; private set; }

    public TurnOnOffSettings Settings { get; private set; }
}
