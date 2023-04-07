using Modding;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Satchel.BetterMenus;
using SFCore.Utils;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;

public class RadianceTeleportControl : Mod, ICustomMenuMod, ILocalSettings<LocalSettings>{
    internal static RadianceTeleportControl instance;
    private PlayMakerFSM absRadControl;
    private Menu menuRef, platsPhaseMenu, finalPhaseMenu = null;
    public static Dictionary<string, bool> platsPhaseDefaults = new Dictionary<string, bool>() {
        { "farLeft", true },
        { "farLeftUpper", true },
        { "sideMidLeft", true },
        { "topMidLeft", true },
        { "topMid", true },
        { "bottomMid", true },
        { "topMidRight", true },
        { "sideMidRight", true },
        { "farRightUpper", true },
        { "farRight", true },
    };
    private static Dictionary<string, string> platsKeyToBtnText = new Dictionary<string, string>() {
        { "farLeft", "Far Left" },
        { "farLeftUpper", "Far Left Upper" },
        { "sideMidLeft", "Left Lower Mid" },
        { "topMidLeft", "Left Top Mid" },
        { "topMid", "Top Mid" },
        { "bottomMid", "Bottom Mid"},
        { "topMidRight", "Right Top Mid" },
        { "sideMidRight", "Right Lower Mid" },
        { "farRightUpper", "Far Right Upper" },
        { "farRight", "Far Right" },
    };
    public static Dictionary<string, bool> finalPhaseDefaults = new Dictionary<string, bool>() {
        { "left", true },
        { "mid", true },
        { "right", true },
    };
    private static Dictionary<string, string> finalPhaseKeyToBtnText = new Dictionary<string, string>() {
        { "left", "Left" },
        { "mid", "Middle" },
        { "right", "Right" },
    };
    private static string[] platsTeleportOrdering = new string[] {"bottomMid", "sideMidRight", "farRight", "farRightUpper", "topMidRight", "topMid", "sideMidLeft", "topMidLeft", "farLeftUpper", "farLeft"};
    private static string[] finalPhaseTeleportOrdering = new string[] {"mid", "left", "right"};

    public RadianceTeleportControl() : base("Radiance Teleport Control") {
       instance = this;
    }

    public override void Initialize(Dictionary<string, Dictionary<string, GameObject>> preloadedObjects) {
        Log("Initializing");

        instance = this;
        On.PlayMakerFSM.OnEnable += OnFsmEnable;

        Log("Initialized");
    }

    public static LocalSettings localSettings { get; private set; } = new();
    public void OnLoadLocal(LocalSettings s) => localSettings = s;
    public LocalSettings OnSaveLocal() => localSettings;

    public MenuScreen GetMenuScreen(MenuScreen modListMenu, ModToggleDelegates? toggleDelegates) {
        Element[] platsPhaseMenuElems = new Element[] {
            new MenuButton(
                name: "Enable All",
                description: "Also reinstates same-spot teleport prevention",
                submitAction: _ => ResetPlatformPhase()
            )
        }.Concat(platsPhaseDefaults.Keys.Select((key) => (
            new HorizontalOption(
                name: platsKeyToBtnText[key],
                description: "",
                applySetting: index => {
                    localSettings.platsPhase[key] = (index == 0 ? true : false);
                    ChangeTeleports();
                },
                loadSetting: () => localSettings.platsPhase[key] ? 0 : 1,
                values: new[] {"Enabled", "Disabled"},
                Id: key
            )
        )).ToArray<Element>()).ToArray<Element>();
        
        platsPhaseMenu ??= new Menu(
            name: "Platform Phase Teleports",
            elements: platsPhaseMenuElems
        );

        Element[] finalPhaseMenuElems = new Element[] {
            new MenuButton(
                name: "Enable All",
                description: "Also reinstates same-spot teleport prevention",
                submitAction: _ => ResetFinalPhase()
            )
        }.Concat(finalPhaseDefaults.Keys.Select((key) => (
            new HorizontalOption(
                name: finalPhaseKeyToBtnText[key],
                description: "",
                applySetting: index => {
                    localSettings.finalPhase[key] = (index == 0 ? true : false);
                    ChangeTeleports();
                },
                loadSetting: () => localSettings.finalPhase[key] ? 0 : 1,
                values: new[] {"Enabled", "Disabled"},
                Id: key
            )
        )).ToArray<Element>()).ToArray<Element>();
        
        finalPhaseMenu ??= new Menu(
            name: "Final Phase Teleports",
            elements: finalPhaseMenuElems
        );
        
        menuRef ??= new Menu(
            name: "Radiance Teleports",
            elements: new Element[] {
                Blueprints.NavigateToMenu(
                    name: "Platform Phase",
                    description: "",
                    getScreen: () => platsPhaseMenu.GetMenuScreen(menuRef.menuScreen)
                ),
                Blueprints.NavigateToMenu(
                    name: "Final Phase",
                    description: "",
                    getScreen: () => finalPhaseMenu.GetMenuScreen(menuRef.menuScreen)
                ),
                new MenuButton(
                    name: "Enable All",
                    description: "Also reinstates same-spot teleport prevention",
                    submitAction: _ => { ResetPlatformPhase(); ResetFinalPhase(); }
                )
            }
        );
        
        return menuRef.GetMenuScreen(modListMenu);
    }

    private void OnFsmEnable(On.PlayMakerFSM.orig_OnEnable orig, PlayMakerFSM self) {
        orig(self);

        if (self.FsmName == "Control" && self.gameObject.name == "Absolute Radiance") {
            absRadControl = self;
            ChangeTeleports();
        }
    }

    private void ResetPlatformPhase() {
        localSettings.platsPhase = new Dictionary<string, bool>(platsPhaseDefaults);
        foreach (string key in platsPhaseDefaults.Keys) {
            (platsPhaseMenu.Find(key) as HorizontalOption).Update();
        }

        if (absRadControl) {
            absRadControl.GetAction<SendRandomEvent>("A2 Tele Choice", 0).weights = new FsmFloat[]{1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f};
            AddPlatsTeleportBiases();
        }
    }

    private void ResetFinalPhase() {
        localSettings.finalPhase = new Dictionary<string, bool>(finalPhaseDefaults);
        foreach (string key in finalPhaseDefaults.Keys) {
            (finalPhaseMenu.Find(key) as HorizontalOption).Update();
        }

        if (absRadControl) {
            absRadControl.GetAction<SendRandomEvent>("A2 Tele Choice 2", 0).weights = new FsmFloat[]{1f, 1f, 1f};
            AddFinalPhaseTeleportBiases();
        }
    }

    private void ChangeTeleports() {
        if (!absRadControl) { return; }

        FsmFloat[] platsWeights = new FsmFloat[10];
        FsmFloat[] finalPhaseWeights = new FsmFloat[3];

        for (int i = 0; i < 10; i++) {
            platsWeights[i] = localSettings.platsPhase[platsTeleportOrdering[i]] ? 1f : 0f;
        }
        for (int i = 0; i < 3; i++) {
            finalPhaseWeights[i] = localSettings.finalPhase[finalPhaseTeleportOrdering[i]] ? 1f : 0f;
        }

        absRadControl.GetAction<SendRandomEvent>("A2 Tele Choice", 0).weights = platsWeights;
        absRadControl.GetAction<SendRandomEvent>("A2 Tele Choice 2", 0).weights = finalPhaseWeights;

        if (platsWeights.All(weight => weight.Value == 1f)) {
            AddPlatsTeleportBiases();
        } else {
            RemovePlatsTeleportBiases();
        }
        if (finalPhaseWeights.All(weight => weight.Value == 1f)) {
            AddFinalPhaseTeleportBiases();
        } else {
            RemoveFinalPhaseTeleportBiases();
        }
    }

    private void RemovePlatsTeleportBiases() {
        if (!absRadControl) { return; }
        Modding.Logger.Log("Removing plats biases");

        for (int i = 1; i < 11; i++) {
            absRadControl.GetAction<IntCompare>($"Tele {i}", 0).integer2 = -1;
        }
    }

    private void AddPlatsTeleportBiases() {
        if (!absRadControl) { return; }
        Modding.Logger.Log("Adding plats biases");

        for (int i = 1; i < 11; i++) {
            absRadControl.GetAction<IntCompare>($"Tele {i}", 0).integer2 = i;
        }
    }

    private void RemoveFinalPhaseTeleportBiases() {
        if (!absRadControl) { return; }
        Modding.Logger.Log("Removing final phase biases");

        for (int i = 1; i < 4; i++) {
            absRadControl.GetAction<IntCompare>($"Tele 1{i}", 0).integer2 = -1;
        }
    }

    private void AddFinalPhaseTeleportBiases() {
        if (!absRadControl) { return; }
        Modding.Logger.Log("Adding final phase biases");

        for (int i = 1; i < 4; i++) {
            absRadControl.GetAction<IntCompare>($"Tele 1{i}", 0).integer2 = i;
        }
    }

    public override string GetVersion() => GetType().Assembly.GetName().Version.ToString();

    public bool ToggleButtonInsideMenu => false;
}

public class LocalSettings {
    public Dictionary<string, bool> platsPhase = new Dictionary<string, bool>(RadianceTeleportControl.platsPhaseDefaults);
    public Dictionary<string, bool> finalPhase = new Dictionary<string, bool>(RadianceTeleportControl.finalPhaseDefaults);
}
