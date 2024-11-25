using Reloaded.Mod.Interfaces.Internal;
using RNSReloaded.Interfaces;
using RNSReloaded.Interfaces.Structs;
using static System.Diagnostics.Debug;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Collections.Frozen;
using System.Xml.Linq;
using System.Reflection;
namespace RNSReloaded.Randomizer;

//At some point I'm going to create a wrapper class over IRNSReloaded to have safe accessor
public unsafe class Randomizer {
    public struct HBSInfoData {
        public string id;
        public string strId;
        public string name;
        public string description;
        // 15 more RValues
    }
    
    public struct AllyData {
        [GMIndex(0)]
        public string id;
        [GMIndex(1)]
        public string name;
        [GMIndex(2)]
        public string description;
        //3 ints
        // a bunch of id strings for moves and upgrades
        // 4 moves * (1 normal + 5 upgrades)

        public override readonly string ToString() {
            return $"Ally{{{this.id}, {this.name}, {this.description}}}";
        }
    }

    // the items data array is of the format [ [ItemData: unlocked, ItemData: locked] ]
    public struct ItemData {
        [GMIndex(0)]
        public string id;
        int smth; //probably to do with the set but I need to actually check
        [GMIndex(2)]
        public string name;
        [GMIndex(3)]
        public string description;
        //14 ints
        // string ? is blank
        //2 ints
    }
    public struct CombinedItemData {
        [GMIndex(0)]
        public ItemData unlocked;
        [GMIndex(1)]
        public ItemData? locked;
    }


    public struct EnemyData {
        string id;
        string name;
        string description;
        string anim;
        //2 large nums, 2 ints, 1 float, 3 ints
    }

     struct TrinketData {
        string id;
        string name;
        string description;
        string unlockCondition;
        // a bunch of stuff
    }

    struct NPCInfo {
        string id;
        string fullname;
        string name;
        string animData;
        //ints
    }

    private Dictionary<string, string> completeMap = [];
    public FrozenDictionary<string, string> CompleteMap { get => this.completeMap.ToFrozenDictionary(); }
    private Dictionary<string, string> languageMap = [];
    public FrozenDictionary<string, string> LanguageMap { get => this.languageMap.ToFrozenDictionary(); }
    private Dictionary<string, AllyData> allyDataMap = [];
    private List<AllyData> allyData = [];


    private List<CombinedItemData> itemData = [];
    public Randomizer() {

    }

    public void Randomize(
        IRNSReloaded rns,
        ILoggerV1 logger
        ) {
        //Get all the strings that need to be randomized
        //put them in a map ( I guess it could just be an array )
        //  optional: based on some filter
        this.LoadLanguageMap(rns);
        this.LoadAllData(rns);
        //shuffle said map
        //  optional: maybe this step is based on a filter
        //for each entry
        //reverse the map key to the data source
        logger.PrintMessage($"Loaded {this.languageMap.Count} values into the lang map", Color.Wheat);
        logger.PrintMessage($"Loaded {this.allyData.Count} value into allydata", Color.Wheat);
        
        Utils.Print($"Loaded {this.itemData.Count} values into itemData");

        this.populateCompleteMap();
        Utils.Print($"Complete map has {this.completeMap.Count} values");

        //foreach (var kvp in this.completeMap) {
        //    Utils.Print($"({kvp.Key}, {kvp.Value})");
        //}
        var toApply = this.randomize(val => val.Length != 0 && !val.Contains("_"));
        this.ApplyChanges(toApply, rns);

    }


    public void LoadLanguageMap(IRNSReloaded rns) {
        //o h my god do it manually, then tidy it later
        //var global = rns.GetGlobalInstance();
        //var langMap = rns.FindValue(global, "languageMap");
        ////something is broken
        //var currentKey = rns.ExecuteCodeFunction("ds_map_find_first", null, null, [*langMap]).GetValueOrDefault();

        //while (currentKey.Type != RValueType.Undefined) {
        //    var value = rns.ExecuteCodeFunction("ds_map_find_first", null, null, [*langMap, currentKey]).GetValueOrDefault();
        //    if (value.Type == RValueType.String) {
        //        this.languageMap[rns.GetString(&currentKey)] = rns.GetString(&value);
        //    }
        //    currentKey = rns.ExecuteCodeFunction("ds_map_find_next", null, null, [*langMap, currentKey]).GetValueOrDefault();
        //}

        GMLDSMap langMapObj = new("languageMap");
        var langMapStrings = new Dictionary<string, string>();
        var langMapData = langMapObj.Collect();
        foreach (var (key, value) in langMapData) {
            var stringKey = key.ToString();
            var stringValue = value.ToString();
            langMapStrings[stringKey] = stringValue;
        }

        this.languageMap = langMapStrings;
    }

    private void LoadData<T>(IRNSReloaded rns, string dataKey, Func<RValue, IRNSReloaded, T> populateData, List<T> dataArray) where T : new() {
        var global = rns.GetGlobalInstance();
        var dataMap = rns.FindValue(global, dataKey);

        Assert(dataMap->Type == RValueType.Array);
        var lengthRValue = rns.ArrayGetLength(dataMap).GetValueOrDefault();
        Assert(lengthRValue.Type == RValueType.Real, $"Expected a Real type for the length of {dataKey} array.");
        int length = (int) lengthRValue.Real;
        dataArray.Clear();
        for (int i = 0; i < length; i++) {
            var entry = rns.ArrayGetEntry(dataMap, i);
            dataArray.Add(populateData(*entry, rns));

        }

    }

    private void LoadData<T>(IRNSReloaded rns, string dataKey, Func<RValue, IRNSReloaded, T> populateData, Dictionary<string, T> dataDictionary) where T : new() {
        var global = rns.GetGlobalInstance();
        var dataMap = rns.FindValue(global, dataKey);
        var lengthRValue = rns.ArrayGetLength(dataMap).GetValueOrDefault();
        Assert(lengthRValue.Type == RValueType.Real, $"Expected a Real type for the length of {dataKey} array.");
        int length = (int) lengthRValue.Real;
        for (int i = 0; i < length; i++) {
            var entry = rns.ArrayGetEntry(dataMap, i);
            var id = rns.GetString(rns.ArrayGetEntry(entry, 0));
            dataDictionary[id] = populateData(*entry, rns); ;
        }
    }

    private AllyData PopulateAllyData( RValue entry, IRNSReloaded rns) {
        AllyData allyData = new();

        allyData.id = rns.GetString(rns.ArrayGetEntry(&entry, 0));

        allyData.name = rns.GetString(rns.ArrayGetEntry(&entry, 1));

        allyData.description = rns.GetString(rns.ArrayGetEntry(&entry, 2));

        return allyData;
    }

    private ItemData PopulateItemData(RValue entry, IRNSReloaded rns) {
        ItemData itemData = new();
        itemData.id = rns.GetString(rns.ArrayGetEntry(&entry, 0));
        itemData.name = rns.GetString(rns.ArrayGetEntry(&entry, 2));
        itemData.description = rns.GetString(rns.ArrayGetEntry(&entry, 3));
        return itemData;
    }
    private CombinedItemData PopulateCombinedItemData(RValue entry, IRNSReloaded rns) {
        CombinedItemData combItem = new();
        combItem.unlocked = this.PopulateItemData(*rns.ArrayGetEntry(&entry, 0), rns);
        if (rns.ArrayGetLength(&entry).GetValueOrDefault().Real == 2) {
            combItem.locked = this.PopulateItemData(*rns.ArrayGetEntry(&entry, 1), rns);
        } else {
            combItem.locked = null;
        }
        return combItem;
    }
    public void LoadAllData(IRNSReloaded rns) {
        this.LoadData(rns, "allyData", this.PopulateAllyData, this.allyData);
        this.LoadData(rns, "itemData", this.PopulateCombinedItemData, this.itemData);
    }

    public void PopulateDataInCompleteMap<T>(string dataName, List<T> toLoad) where T: notnull {
        for (var i = 0; i < toLoad.Count; i++) {
            this.PopulateStructInCompleteMap(dataName + "@" + i, toLoad[i]);
        }
    }
    public void PopulateStructInCompleteMap(string prefix, object toPopulate) {
        if (toPopulate == null) {
            return;
        }
        var type = toPopulate.GetType();
        foreach (var prop in type.GetFields()) {
            if (prop.GetCustomAttribute<GMIndexAttribute>() is GMIndexAttribute indexAttr) {
                var propType = prop.FieldType;
                var lookup = prefix + "@" + indexAttr.index;
                if (propType == typeof(string)) {
                    if (prop.GetValue(toPopulate) is string value) {
                        this.completeMap[lookup] = value;
                        Utils.Print(lookup);
                    }
                } else if (propType.IsValueType) {
                    var nestedVal = prop.GetValue(toPopulate);
                    if (nestedVal != null) {
                        this.PopulateStructInCompleteMap(lookup, nestedVal);
                    }
                }
            }
        }
    }
    public void populateCompleteMap() {
        //languageMap
        foreach (var (key, val) in this.LanguageMap) {
            this.completeMap[$"languageMap@{key}"] = val;
        }

        //allyMap
        this.PopulateDataInCompleteMap("allyData", this.allyData);
        this.PopulateDataInCompleteMap("itemData", this.itemData);


    }
    public static Predicate<T> CombinedPredicate<T>(List<Predicate<T>> predicates) {
        return item => predicates.All(p => p(item));
    }

    public Dictionary<string, string> randomize(Predicate<string> rule) {
        //var keys = this.completeMap.Keys.ToArray();
        //var values = this.completeMap.Values.ToArray();
        var selected = this.completeMap.Where(kv => rule(kv.Value));
        var keys = selected.Select(kv => kv.Key).ToArray();
        var values = selected.Select(kv => kv.Value).ToArray();
        Utils.random.Shuffle(values);
        var result = new Dictionary<string, string>();

        for(int i = 0; i < keys.Length; i++) {
            result[keys[i]] = values[i];
        }

        return result;
    }
    private void ApplyChanges(Dictionary<string, string> toApply, IRNSReloaded rns) {
        foreach ( var (key, value) in toApply) {
            //Utils.Print($"Making Change {key} to {value}");
            this.makeChange(key, value, rns);
        }
    }

    private unsafe void makeChange(string key, string value, IRNSReloaded rns) {
        string[] keys = key.Split("@");
        RValue prevVal = *Utils.GetGlobalValue(keys[0]);
        for (int i = 1; i < keys.Length-1; i++) {
            if (int.TryParse(keys[i], out var ind)) {
                prevVal = *rns.ArrayGetEntry(&prevVal, ind);
            } else {
                GMLDSMap map = new(prevVal);
                RValue tmp = new RValue();
                rns.CreateString(&tmp, keys[ind]);
                prevVal = map.FindValue(tmp);

            }
            //Utils.Print($"current val is {prevVal.ToString()}");
        }
        string lastKey = keys.Last();
        //use func to set last
        if (int.TryParse(lastKey, out var index)) {
            rns.CreateString(rns.ArrayGetEntry(&prevVal, index), value);
        } else {
            GMLDSMap map = new(prevVal);
            RValue tmp = new RValue();
            rns.CreateString(&tmp, lastKey);
            RValue tmpVal = new RValue();
            rns.CreateString(&tmpVal, value);
            map.Set(tmp, tmpVal);

        }
    }
}

internal class GMIndexAttribute : Attribute {
    public int index {  get; }
    public GMIndexAttribute() { }
    public GMIndexAttribute(int index) {
        this.index = index;
    }

}
