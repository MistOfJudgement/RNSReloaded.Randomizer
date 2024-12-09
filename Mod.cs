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
using RNSReloaded.Randomizer.Config;
using System.Text.RegularExpressions;
namespace RNSReloaded.Randomizer;

public unsafe class Mod : IMod {
    private Configurator? configurator;
    private Config.Config config = null!;
    private WeakReference<IRNSReloaded>? rnsReloadedRef;
    private WeakReference<IReloadedHooks>? hooksRef;
    private ILoggerV1 logger;
    private IHook<ScriptDelegate> langHook;
    private readonly Dictionary<string, IHook<ScriptDelegate>> hookedFunctions = new();
    private Randomizer randomizer = new();
    private readonly string[] warningFunctionNames = [
        "scrbp_warning_msg_enrage",
        "scrbp_warning_msg_hbs",
        "scrbp_warning_msg_p",
        "scrbp_warning_msg_pos",
        "scrbp_warning_msg_t",
        ];
    public void StartEx(IModLoaderV1 loader, IModConfigV1 modConfig) {
        
        this.configurator = new Configurator(((IModLoader) loader).GetModConfigDirectory(modConfig.ModId));
        this.config = this.configurator.GetConfiguration<Config.Config>(0);
        this.config.ConfigurationUpdated += this.ConfigurationUpdated;

        this.hooksRef = loader.GetController<IReloadedHooks>();
        this.rnsReloadedRef = loader.GetController<IRNSReloaded>();
        this.logger = loader.GetLogger();
        Utils.logger = this.logger;
        if (this.rnsReloadedRef.TryGetTarget(out var rnsReloaded)) {
            rnsReloaded.OnReady += this.Ready;
        }
    }

    private void ConfigurationUpdated(IUpdatableConfigurable newConfig) {
        this.config = (Config.Config) newConfig;
    }

    
    public void Ready() {
        if ((this.hooksRef != null && this.hooksRef.TryGetTarget(out var hooks)) &&
                (this.rnsReloadedRef != null && this.rnsReloadedRef.TryGetTarget(out var rns))) {
            var id = rns.ScriptFindId("scr_stringsprite_load_all");
            var data = rns.GetScriptData(id - 100_000);
            this.langHook = hooks.CreateHook<ScriptDelegate>(this.LangStringDetour, data->Functions->Function);
            this.langHook.Activate();
            this.langHook.Enable();
            this.logger.PrintMessage("Enabled Hook", Color.Red);
            foreach (var warningFunc in this.warningFunctionNames) {
                this.CreateWarningHook(warningFunc);
            }
        }
        this.logger.PrintMessage("Done Ready", Color.Red);


    }

    private void CreateWarningHook(string funcName) {
        if ((this.hooksRef != null && this.hooksRef.TryGetTarget(out var hooks)) &&
                (this.rnsReloadedRef != null && this.rnsReloadedRef.TryGetTarget(out var rns))) {
            var id = rns.ScriptFindId(funcName);
            var data = rns.GetScriptData(id - 100_000);

            this.hookedFunctions[funcName] = hooks.CreateHook(this.WarningDetour(funcName), data->Functions->Function);
            this.hookedFunctions[funcName].Activate();
            this.hookedFunctions[funcName].Enable();
        }
    }

    private ScriptDelegate WarningDetour(string funcName) {
        return (self, other, ret, argc, argv) => {
            if (this.rnsReloadedRef != null && this.rnsReloadedRef.TryGetTarget(out var rns)) {
                var rules = new List<Predicate<KeyValuePair<string, string>>> {
                    kvp => kvp.Key.StartsWith("languageMap@eff_") || (kvp.Key.StartsWith("hbsInfo@") && kvp.Key.EndsWith("2")),
                    kvp => kvp.Value.Length > 0,
                };
                this.randomizer.Randomize(rns, this.logger, rules);
                Utils.Print($"Randomizing from {funcName}");
                RValue unused;
                this.langHook.OriginalFunction.Invoke(self, other, &unused, 0, null);
            }
            this.hookedFunctions[funcName].OriginalFunction.Invoke(self, other, ret, argc, argv);
            return ret;
        };
    }

    private RValue* LangStringDetour(CInstance* self,
                                     CInstance* other,
                                     RValue* ret,
                                     int argc,
                                     RValue** argv) {
        if (this.rnsReloadedRef != null && this.rnsReloadedRef.TryGetTarget(out var rns)) {
            this.randomizer.Load(rns);
            if (this.config.ShowStringIds) {
               this.randomizer.ReplaceWithStrId(rns);
            } else {
                if (!this.config.OnlyRandomizeBattleEffects) {
                    var rules = new List<Predicate<KeyValuePair<string, string>>> {
                    kvp => kvp.Value.Length > 0,
                    kvp => !kvp.Value.Contains("_"),
                    //kvp => kvp.Key.StartsWith("hbs")
                };
                    if (this.config.OnlyRandomizeBattleEffects) {
                        rules.Add(kvp => kvp.Key.StartsWith("languageMap@eff_") || (kvp.Key.StartsWith("hbsInfo@") && kvp.Key.EndsWith("2")));
                    }
                    this.randomizer.Randomize(rns, this.logger, rules);
                }
            }
        }
        this.langHook.OriginalFunction.Invoke(self, other, ret, argc, argv);
        return ret;
    }
    public void Suspend() {
    }

    public void Resume() {
    }

    public bool CanSuspend() => true;

    public void Unload() { }
    public bool CanUnload() => false;

    public Action Disposing => () => { };
}
