using Robust.Shared.GameStates;

namespace Content.Shared.Stacks;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class StackSignatureComponent : Component
{
    [DataField("signature"), AutoNetworkedField]
    public string Signature = string.Empty;
}
