using Content.Shared.Whitelist;

namespace Content.Shared.Trigger.Components.Triggers;

public sealed partial class TriggerOnCollideComponent
{
    /// <summary>
    ///     Doesn't trigger on an entity that passes this. BF14's anti-tank rockets don't go off on people.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityWhitelist? Blacklist;
}
