using Reloaded.Hooks.Definitions;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;
using RNSReloaded.Interfaces;
using RNSReloaded.Interfaces.Structs;
using System.Collections;
using System.Drawing;
using System.Text.Json;
using System.Runtime.InteropServices;
using System.Diagnostics;
using RNSReloaded;
using System.Runtime.ExceptionServices;
namespace RNSReloaded.Randomizer;

public unsafe class Mod : IMod {

    private readonly string[] idsToRandomize = {
        "eff_knockback_warning",
        "eff_spread_warning",
        "eff_soak_warning",
        "eff_soak_warning_2",
        "eff_soak_warning_3",
        "eff_aoe_warning",
        "eff_tele_warning",
        "eff_moving",
        "eff_colormatch",
        "eff_colormatch2",
        "eff_enrage",
        "eff_movement_stop",
        "eff_movement_move",
        "eff_thorns",
        "eff_cleave",
        "eff_steelyourself",
    };
    private static readonly Random Random = new Random();
    private WeakReference<IRNSReloaded>? rnsReloadedRef;
    private WeakReference<IReloadedHooks>? hooksRef;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private IRNSReloaded rnsReloaded;
    private IReloadedHooks hooks;
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    private ILoggerV1 logger = null!;
    private Dictionary<string, string> localLanguageMap = new Dictionary<string, string>();
    private IHook<ScriptDelegate>? encounterHook;
    private IHook<ScriptDelegate>? langStringsInitHook;
    private IHook<ScriptDelegate>? makeWarningHookT;
    private IHook<ScriptDelegate>? makeWarningHookP;

    public void StartEx(IModLoaderV1 loader, IModConfigV1 modConfig) {
        this.hooksRef = loader.GetController<IReloadedHooks>()!;
        this.rnsReloadedRef = loader.GetController<IRNSReloaded>();

        this.logger = loader.GetLogger();
        if (this.rnsReloadedRef.TryGetTarget(out var rnsReloaded)) {
            rnsReloaded.OnReady += this.Ready;
            this.rnsReloaded = rnsReloaded;
        }
    }

    public RValue CreateString(string str) {
        RValue result;
        this.rnsReloaded.CreateString(&result, str);
        return result;
    }

    private void log(string message) {
        this.logger.PrintMessage(message, Color.Wheat);
    }

    public void Ready() {
        if (this.hooksRef != null && this.hooksRef.TryGetTarget(out var hooks)) {
            this.hooks = hooks;
            //this.encounterHook = this.hookScript("scrdt_encounter", this.EncounterDetour);
            this.langStringsInitHook = this.hookScript("scr_lang_strings_init", this.InitStringsDetour);
            this.makeWarningHookT = this.hookScript("scrbp_warning_msg_t", this.WarningDetour(()=>this.makeWarningHookT));
            this.makeWarningHookP = this.hookScript("scrbp_warning_msg_p", this.WarningDetour(()=> this.makeWarningHookP));
        } else {
            this.log("Unable to setup hooks, exiting..");
            throw new Exception("Failed to get rnsReloaded");
        }
    }
    private ScriptDelegate WarningDetour(Func<IHook<ScriptDelegate>> originalFunction) {
        return (self, other, ret, argc, argv) => {
            this.randomizeInMap();
            this.rnsReloaded.ExecuteScript("scr_stringsprite_load_all", self, other, 0, null);
            this.log("refreshing");
                return originalFunction().OriginalFunction(self, other, ret, argc, argv);
        };
    }
    private RValue* InitStringsDetour(CInstance* self, CInstance* other, RValue* returnValue, int argc, RValue** argv) {
        var ret = this.langStringsInitHook!.OriginalFunction(self, other, returnValue, argc, argv);
        this.loadLocalLanguageMap();
        return ret;
    }

    private IHook<ScriptDelegate> hookScript(string script, ScriptDelegate scriptDelegate) {
        var id = this.rnsReloaded.ScriptFindId(script);
        var scriptData = this.rnsReloaded.GetScriptData(id - 100000);
        var output = this.hooks.CreateHook(scriptDelegate, scriptData->Functions->Function);
        output.Activate();
        output.Enable();
        return output;
    }

    private static E pickRandom<E>(E[] array) {
        return array[Random.Next(0, array.Length)];
    }

    private void loadLocalLanguageMap() {
        var languageMap = *this.GetGlobalVar("languageMap");
        var first = this.rnsReloaded.ExecuteCodeFunction("ds_map_find_first", null, null, [languageMap]) ?? null;
        while (first.Type != RValueType.Undefined) {
            var val = this.rnsReloaded.ExecuteCodeFunction("ds_map_find_value", null, null, [languageMap, first]) ?? null;
            this.localLanguageMap[this.rnsReloaded.GetString(&first)] = this.GetString(val);
            first = this.rnsReloaded.ExecuteCodeFunction("ds_map_find_next", null, null, [languageMap, first]) ?? null;
        }
        this.log($"Loaded {this.localLanguageMap.Count} values");
    }

    private void randomizeInMap() {
        var existingVals = new string[this.idsToRandomize.Length];
        for (int i = 0; i < this.idsToRandomize.Length; i++) {
            existingVals[i] = pickRandom(this.localLanguageMap.Values.ToArray());
        }
        for (int i = 0; i < existingVals.Length; i++) {
            this.dsMapSet("languageMap", this.idsToRandomize[i], existingVals[i]);
            this.log($"setting {this.idsToRandomize[i]} to {existingVals[i]}");
        }
    }
    private string GetString(RValue rvalue) {
        return this.rnsReloaded.GetString(&rvalue);
    }

    public RValue* GetGlobalVar(string key) {
        var instance = this.rnsReloaded.GetGlobalInstance();
        return this.rnsReloaded.FindValue(instance, key);
    }

    private RValue* EncounterDetour(
        CInstance* self, CInstance* other, RValue* returnValue, int argc, RValue** argv
    ) {
        this.log("encounter occuring");
        this.randomizeInMap();
        this.rnsReloaded.ExecuteScript("scr_stringsprite_load_all", self, other, 0, null);

        returnValue = this.encounterHook!.OriginalFunction(self, other, returnValue, argc, argv);
        return returnValue;
    }


    private RValue dsMapGetValue(string map, string key) {
        return this.rnsReloaded.ExecuteCodeFunction("ds_map_find_value", null, null, [*this.GetGlobalVar(map), this.CreateString(key)]) ?? null;
    }

    private void dsMapSet(string map, string key, string val) {
        this.rnsReloaded.ExecuteCodeFunction("ds_map_set", null, null, [*this.GetGlobalVar(map), this.CreateString(key), this.CreateString(val)]);
    }

    public void Suspend() {
        this.encounterHook?.Disable();
    }

    public void Resume() {
        this.encounterHook?.Enable();
    }

    public bool CanSuspend() => true;

    public void Unload() { }
    public bool CanUnload() => false;

    public Action Disposing => () => { };
}
