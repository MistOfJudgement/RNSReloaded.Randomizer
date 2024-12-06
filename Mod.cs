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
        }
        this.logger.PrintMessage("Done Ready", Color.Red);


    }
    private RValue* LangStringDetour(CInstance* self,
                                     CInstance* other,
                                     RValue* ret,
                                     int argc,
                                     RValue** argv) {
        if (this.rnsReloadedRef != null && this.rnsReloadedRef.TryGetTarget(out var rns)) {
            var randomizer = new Randomizer(rns);
            if (this.config.ShowStringIds) {
                randomizer.ReplaceWithStrId(rns);
            } else {
                var rules = new List<Predicate<KeyValuePair<string, string>>> {
                    kvp => kvp.Value.Length > 0,
                    kvp => !kvp.Value.Contains("_"),
                    //kvp => kvp.Key.StartsWith("hbs")
                };
                if (this.config.OnlyRandomizeBattleEffects) {
                    rules.Add(kvp => kvp.Key.StartsWith("languageMap@eff_") || (kvp.Key.StartsWith("hbsInfo@") && kvp.Key.EndsWith("2")));
                }
                randomizer.Randomize(rns, this.logger, rules);
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
