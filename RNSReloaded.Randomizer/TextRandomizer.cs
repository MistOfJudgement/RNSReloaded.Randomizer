using Reloaded.Hooks.Definitions;
using RNSReloaded.Interfaces;
using RNSReloaded.Interfaces.Structs;
using System;
using System.Collections.Generic;

namespace RNSReloaded.Randomizer;

enum DataMap {
    allyDataKeyMap,
    animationDataKeyMap,
    dialogDataKeyMap,
    enemyDataKeyMap,
    hallwayDataKeyMap,
    hbsDataKeyMap,
    itemDataKeyMap,
    npcDataKeyMap,
    projDataKeyMap,
    trinketDataKeyMap,
}

public unsafe class TextRandomizer
{

    private Dictionary<string, string> originalLanguageMap = new();
    private Dictionary<DataMap, Dictionary<string, RValue>> dataMaps = new();
	private List<KeyValuePair<string, string>> toUpdate = new();

	private Config.Config config;
    private Utils utils;
    private IRNSReloaded rnsReloaded => this.utils.rnsReloaded;
    private IReloadedHooks hooks => this.utils.hooks;
    public bool RandomizingBattleText => this.config.RandTextEffects || this.config.RandTextHBS;

    private IHook<ScriptDelegate> langStringInitHook;

    public TextRandomizer(Config.Config conf, Utils utils)
    {
        this.config = conf;
        this.utils = utils;
        this.SetupHooks();
    }

    public void SetupHooks()
	{
        this.langStringInitHook = this.HookScript("scr_lang_strings_init", this.LangStringsInitDetour);
        if(this.RandomizingBattleText) {

        }
	}

    private RValue* LangStringsInitDetour(CInstance* self, CInstance* other, RValue* returnValue, int argc, RValue** argv) {
        var ret = this.langStringInitHook!.OriginalFunction(self, other, returnValue, argc, argv);
        this.LoadLanguageMap();
        return ret;
    }

    private IHook<ScriptDelegate> HookScript(string scriptName, ScriptDelegate scriptFunc) {
        var id = this.rnsReloaded.ScriptFindId(scriptName);
        var scriptData = this.rnsReloaded.GetScriptData(id - 100_000);
        var output = this.hooks.CreateHook(scriptFunc, scriptData->Functions->Function);
        output.Activate();
        output.Enable();
        return output;
    }


    private void RandomizeTextWithinCategories() {
        // Determine categories
        // For each category
        //  For each key
        //      pick a random key
        //      add the old key and the random value to the new array
        // load array


    }

    private void ApplyMapping() {
        RValue languageMap = this.utils.GetGlobalVar("languageMap");
        foreach(var (key, value) in this.toUpdate) {
            this.utils.dsMapSet(languageMap, key, value);
            //Also need to set the stringsprite
        }
        this.toUpdate.Clear();
    }

	private void LoadLanguageMap()
	{
        var languageMap = this.utils.GetGlobalVar("languageMap");
        var first = this.rnsReloaded.ExecuteCodeFunction("ds_map_find_first", null, null, [languageMap]) ?? null;
        while (first.Type != RValueType.Undefined) {
            var val = this.rnsReloaded.ExecuteCodeFunction("ds_map_find_value", null, null, [languageMap, first]) ?? null;
            this.originalLanguageMap[this.rnsReloaded.GetString(&first)] = this.utils.GetString(val);
            first = this.rnsReloaded.ExecuteCodeFunction("ds_map_find_next", null, null, [languageMap, first]) ?? null;
        }
        this.utils.Log($"Loaded {this.originalLanguageMap.Count} values");
    }

}
