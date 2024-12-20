using System.ComponentModel;

namespace RNSReloaded.Randomizer.Config;

public class Config : Configurable<Config> {

    [Category("TextRandomizer"), DefaultValue(false)]
    [Description("Remaps all text to show the string ids. Has problems and could crash")]
    public bool ShowStringIds { get; set; } = false;

    [Category("TextRandomizer"), DefaultValue(true)]
    [Description("Only Randomize text that warns/informs the player")]
    public bool OnlyRandomizeBattleEffects { get; set; } = true;

}
