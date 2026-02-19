using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._HL.Brainwashing;

[RegisterComponent]
public sealed partial class BrainwashVizorComponent : Component
{
    [DataField(serverOnly: true)]
    public DoAfterId? DoAfter;
}

public abstract class SharedBrainwashVizorSystem : EntitySystem;

[Serializable, NetSerializable]
public sealed partial class EngagedEvent : SimpleDoAfterEvent
{
    public EngagedEvent(NetEntity wearer)
    {
        Wearer = wearer;
    }
    [DataField]
    public NetEntity Wearer;
}

[Serializable, NetSerializable]
public sealed class BrainwashedEvent : EntityEventArgs;
