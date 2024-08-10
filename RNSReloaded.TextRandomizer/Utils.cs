using Reloaded.Hooks.Definitions;
using Reloaded.Mod.Interfaces.Internal;
using RNSReloaded.Interfaces;
using RNSReloaded.Interfaces.Structs;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RNSReloaded.Randomizer;
public unsafe class Utils {
    public IRNSReloaded rnsReloaded { get; private set; }
    public IReloadedHooks hooks { get; private set; }
    public ILoggerV1 logger { get; private set; }
    public static Random random { get; private set; } = new Random();

    public Utils(IRNSReloaded rnsReloaded, IReloadedHooks hooks, ILoggerV1 logger) {
        this.rnsReloaded = rnsReloaded;
        this.hooks = hooks;
        this.logger = logger;
    }


    public RValue CreateString(string value) {
        RValue result;
        this.rnsReloaded.CreateString(&result, value);
        return result;
    }

    public string GetString(RValue rvalue) {
        return this.rnsReloaded.GetString(&rvalue);
    }

    public void Log(string message) {
        this.logger.PrintMessage(message, Color.Wheat);
    }

    public RValue GetGlobalVar(string key) {
        var instance = this.rnsReloaded.GetGlobalInstance();
        return *this.rnsReloaded.FindValue(instance, key);
    }
    public Dictionary<string, RValue> CollectMap(string mapId) {
        Dictionary<string, RValue> output = new Dictionary<string, RValue>();
        var mapVal = this.GetGlobalVar(mapId);
        var current = this.rnsReloaded.ExecuteCodeFunction("ds_map_find_first", null, null, [mapVal]).GetValueOrDefault();
        while (current.Type != RValueType.Undefined) {
            var value = this.dsMapFindValue(mapVal, current);
            output[this.GetString(current)] = value;
            current = this.rnsReloaded.ExecuteCodeFunction("ds_map_find_next", null, null, [mapVal, current]).GetValueOrDefault();
        }
        return output;
    }

    public Dictionary<string, T> CollectMap<T>(string mapId, Func<KeyValuePair<string, RValue>,T> processFunc) {
        var map = this.CollectMap(mapId);
        Dictionary<string, T> output = new Dictionary<string, T>();
        foreach (var item in map) {
            output[item.Key] = processFunc(item);
        }
        return output;
    }

    public void applyMap(string mapId, Dictionary<string, RValue> changes) {
        RValue map = this.GetGlobalVar(mapId);
        foreach (var (key, value) in changes) {
            this.dsMapSet(map, key, value);
        }
    }

    public RValue dsMapFindValue(RValue mapId, RValue key) {
        return this.rnsReloaded.ExecuteCodeFunction("ds_map_find_value", null, null, [mapId, key]).GetValueOrDefault();
    }
    public RValue dsMapFindValue(RValue mapId, string key) {
        return this.dsMapFindValue(mapId, this.CreateString(key));
    }

    public RValue dsMapFindValue(string map, string key) {
        return this.dsMapFindValue(this.GetGlobalVar(map), key);
    }

    public void dsMapSet(RValue mapId, string key, RValue value) {
        this.rnsReloaded.ExecuteCodeFunction("ds_map_set", null, null, [mapId, this.CreateString(key), value]);
    }
    public void dsMapSet(RValue mapId, string key, string val) {
        this.dsMapSet(mapId, key, this.CreateString(val));
    }
    public void dsMapSet(string map, string key, string val) {
        this.dsMapSet(this.GetGlobalVar(map), key, val);
    }

    public static void Shuffle<T>(List<T> list) {
        for (int i = 0; i < list.Count; i++) {
            T tmp = list[i];
            int rand = random.Next(0, list.Count - i);
            list[i] = list[rand];
            list[rand] = tmp;
        }
    }
}

