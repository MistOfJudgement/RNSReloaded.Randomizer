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
        "d_nothing",
        "d_cat_shopkeeper_0_0"
    };
    private static Random RNG = new Random();
    private WeakReference<IRNSReloaded>? rnsReloadedRef;
    private WeakReference<IReloadedHooks>? hooksRef;
    private WeakReference<IStartupScanner>? scannerRef;
    IRNSReloaded rnsReloaded;
    IReloadedHooks hooks;
    private ILoggerV1 logger = null!;
    private Dictionary<string, string> localLanguageMap = new Dictionary<string, string>();
    private IHook<ScriptDelegate>? encounterHook;
    private IHook<ScriptDelegate>? stringTranslateHook;
    private IHook<whoTFKnows>? idkHook;
    private delegate void whoTFKnows(ulong p1, char* p2);

    private nint baseAddress;
    public void StartEx(IModLoaderV1 loader, IModConfigV1 modConfig) {
        this.hooksRef = loader.GetController<IReloadedHooks>()!;
        this.scannerRef = loader.GetController<IStartupScanner>()!;
        this.rnsReloadedRef = loader.GetController<IRNSReloaded>();

        this.logger = loader.GetLogger();
        if (this.rnsReloadedRef.TryGetTarget(out var rnsReloaded)) {
            rnsReloaded.OnReady += this.Ready;
            this.rnsReloaded = rnsReloaded;
        }
        
    }
    private void AfterStart() {
        //int globalObjOffset = 0x21e_5c_78;
        //CInstance* globalObj = (CInstance*) Marshal.ReadIntPtr(this.baseAddress + globalObjOffset);
        //this.log($"obj add {(nint)globalObj:X} : offset {globalObjOffset:X}");
        ////globalObj->obj.vtable->internalGetYYVarRef(&globalObj->obj, 4);
        ////this.log($"Trying thing {*globalObj}");

    }
    public RValue CreateString(string str) {
        RValue result;
        if (this.rnsReloadedRef?.TryGetTarget(out var rnsReloaded) ?? false) {
            rnsReloaded.CreateString(&result, str);
            return result;
        }
        this.log("Failed to create string: " + str);
        result = new RValue();
        result.Type = RValueType.Undefined;
        return result;
    }

    private void log(string message) {
        this.logger.PrintMessage(message, Color.Wheat);
    }
    private void whoKnowsHowThisWorks(ulong p1, char* p2) {
        if (this.rnsReloadedRef != null && this.rnsReloadedRef.TryGetTarget(out var rnsReloaded)) {
            this.logger.PrintMessage($"my thing {p1}, {new String(p2)}", Color.Green);
        }
        this.idkHook!.OriginalFunction(p1, p2);
    }
    public void Ready() {
        if (
            this.rnsReloadedRef != null
            && this.rnsReloadedRef.TryGetTarget(out var rnsReloaded)
            && this.hooksRef != null
            && this.hooksRef.TryGetTarget(out var hooks)
            
        ) {
            this.hooks = hooks;
            //rnsReloaded.LimitOnlinePlay();

            //var id = rnsReloaded.ScriptFindId("scrdt_encounter");
            //var script = rnsReloaded.GetScriptData(id - 100000);
            //this.encounterHook =
            //    hooks.CreateHook<ScriptDelegate>(this.EncounterDetour, script->Functions->Function);
            //this.encounterHook.Activate();
            //this.encounterHook.Enable();

            var loadStringsId = rnsReloaded.ScriptFindId("scr_lang_string");
            var stringScript = rnsReloaded.GetScriptData(loadStringsId - 100000);
            this.stringTranslateHook = hooks.CreateHook<ScriptDelegate>(this.StringLookupDetour, stringScript->Functions->Function);
            this.stringTranslateHook.Activate();
            this.stringTranslateHook.Enable();

            this.encounterHook = this.hookScript("scrdt_encounter", this.EncounterDetour);
            this.loadStringspriteHook = this.hookScript("scr_stringsprite_load_all", (self, other, ret, argc, argv) => {
                this.loadLocalLanguageMap();
                this.log(string.Join(", ", this.localLanguageMap.Values.Take(7)));
                return this.loadStringspriteHook!.OriginalFunction(self, other, ret, argc, argv);
            });
            
            //var id = rnsReloaded.ScriptFindId("scr_runmenu_main_setup");
            //var script = rnsReloaded.GetScriptData(id);
            //IHook<ScriptDelegate> hook = null;
            //hook= hooks.CreateHook<ScriptDelegate>((CInstance* self, CInstance* other, RValue* returnValue, int argc, RValue** argv) => {
            //    if (hook == null) return null;
            //    var ret = hook.OriginalFunction(self, other, returnValue, argc, argv);
            //    return ret;
            //}, script->Functions->Function);
            //this.log("Made hook");

        }
    }
    private IHook<ScriptDelegate> hookScript(string script, ScriptDelegate scriptDelegate) {
        var id = this.rnsReloaded.ScriptFindId(script);
        var scriptData = this.rnsReloaded.GetScriptData(id - 100000);
        var output = this.hooks.CreateHook<ScriptDelegate>(scriptDelegate, scriptData->Functions->Function);
        output.Activate();
        output.Enable();
        return output;
    }

    private static E pickRandom<E>(E[] array) {
        return array[RNG.Next(0, array.Length)];
    }

    private string randomizeID(string id) {
        if(this.idsToRandomize.Contains(id)) {
            return pickRandom(this.idsToRandomize);
        }
        return id;
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
    bool loaded = false;
    private CInstance* globalThing;
    private nint effectiveAddress;
    private IHook<ScriptDelegate>? dsMapFindValueHook;
    private IHook<ScriptDelegate>? loadStringspriteHook;

    public RValue* GetGlobalVar(string key) {
        if (this.rnsReloadedRef?.TryGetTarget(out var rnsReloaded) ?? false) {
            var instance = rnsReloaded.GetGlobalInstance();
            return rnsReloaded.FindValue(instance, key);
        }
        return null;
    }
    private void logRValue(RValue rvalue, string tag = "") {
            this.log($"{tag} {rvalue.Type} {this.rnsReloaded.GetString(&rvalue)} {rvalue.Flags}");

    }


    private RValue* StringLookupDetour(CInstance* self, CInstance* other, RValue* returnValue, int argc, RValue** argv) {
        //this.logger.PrintMessage("Detout", Color.White);
        if(this.rnsReloadedRef != null && this.rnsReloadedRef.TryGetTarget(out var rnsReloaded)) {
            if (this.idsToRandomize.Contains(rnsReloaded.GetString(argv[0]))) {
                string randomId = /**this.randomizeID*/(rnsReloaded.GetString(argv[0]));
                //this.logger.PrintMessage($"Input: {rnsReloaded.GetString(argv[0])}, Output: {randomId}", Color.White);
                //this.log($"manual try");
   
                //RValue attempt = rnsReloaded.ExecuteCodeFunction("ds_map_keys_to_array", self, other, [*(this.GetGlobalVar("languageMap"))]).GetValueOrDefault();
                //this.log($"{attempt.Type} {rnsReloaded.GetString(&attempt)}");

                //rnsReloaded.CreateString(argv[0], pickRandom(this.idsToRandomize));


                }
            }
        returnValue = this.stringTranslateHook!.OriginalFunction(self, other, returnValue, argc, argv);
        if(!this.loaded) {
            //this.loaded = true;
            try {
                //IntPtr pointerAddress = Marshal.ReadIntPtr(this.effectiveAddress);
                //this.globalThing = (CInstance*) (pointerAddress.ToInt64());
                //this.log($"Pointer address: {pointerAddress:X}");
                //this.log($"Global thing address: {(nint) this.globalThing:X}");
                //this.log($"uhhhh {((nint)getYYVar(this.globalThing, 4)->Pointer)} is has type {(uint)getYYVar(this.globalThing, 4)->Type}");

            } catch (Exception ex) {
                this.log($"Error reading pointer at {this.effectiveAddress:X}: {ex.Message}");
            }
        }
        //this.log($"self: {(nint) self:X}, other: {(nint) other:X}");
        //this.log($"{(nint) (this.globalThing->obj.vtable):X}");
        //this.log($"{Marshal.GetDelegateForFunctionPointer<InternalGetYYVarRefDelegate>(self->obj.vtable->internalGetYYVarRef)(&self->obj, 4)->Type}");
        //this.AfterStart();
        return returnValue;
    }
    public static RValue* getYYVar(CInstance* instance, int key) {
        return Marshal.GetDelegateForFunctionPointer<InternalGetYYVarRefDelegate>(instance->obj.vtable->internalGetYYVarRef)(&instance->obj, key);
    }
    private RValue* EncounterDetour(
        CInstance* self, CInstance* other, RValue* returnValue, int argc, RValue** argv
    ) {
        this.log("encounter occuring");
        this.randomizeInMap();
        this.loadStringspriteHook!.OriginalFunction(self, other, returnValue, 0, null);

        returnValue = this.encounterHook!.OriginalFunction(self, other, returnValue, argc, argv);
        return returnValue;
    }


    private RValue dsMapGetValue(string map, string key) {
        if (this.rnsReloadedRef?.TryGetTarget(out var rnsReloaded) ?? false) {

            RValue ret = rnsReloaded.ExecuteCodeFunction("ds_map_find_value", null, null, [*this.GetGlobalVar(map), this.CreateString(key)]) ?? null;
            return ret;
        }
            

        return null;
    }

    private void dsMapSet(string map, string key, string val) {
        this.rnsReloaded.ExecuteCodeFunction("ds_map_set", null, null, [*this.GetGlobalVar(map), this.CreateString(key), this.CreateString(val)]);
    }

    public void Suspend() {
        this.stringTranslateHook?.Disable();

    }

    public void Resume() {
        this.stringTranslateHook?.Enable();
    }

    public bool CanSuspend() => true;

    public void Unload() { }
    public bool CanUnload() => false;

    public Action Disposing => () => { };
}
