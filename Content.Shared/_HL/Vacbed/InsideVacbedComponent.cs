using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._HL.Vacbed;

[RegisterComponent]
[NetworkedComponent]
public sealed partial class InsideVacbedComponent : Component
{
    [ViewVariables]
    [DataField("previousOffset")]
    public Vector2 PreviousOffset { get; set; } = new(0, 0);

    [DataField]
    public bool IsMuzzled { get; set; }
}
