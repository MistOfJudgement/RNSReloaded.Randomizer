using System.ComponentModel;

namespace RNSReloaded.Randomizer.Config;

public class Config : Configurable<Config> {

    [Category("TextRandomizer"), DefaultValue(false)]
    [Description("Remaps all text to show the string ids")]
    public bool ShowStringIds { get; set; } = false;

}
