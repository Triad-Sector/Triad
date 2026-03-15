using Content.Shared.Mobs;
using Content.Shared.FixedPoint;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.GameStates;
using Content.Shared.Damage;
using Robust.Shared.Prototypes;
using Content.Shared.Damage.Prototypes;

namespace Content.Shared._HL.Damage;

/// <summary>
/// Passively damages the entity based on their blood level. Scales with higher blood levels.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
public sealed partial class HLBloodLevelPassiveDamageComponent : Component
{
    /// <summary>
    /// The entitys' states that passive damage will apply in
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<MobState> AllowedStates = new();

    [DataField, AutoNetworkedField]
    public List<ProtoId<DamageGroupPrototype>> AllowedDamageGroups = new();

    /// <summary>
    /// Damage / Healing per interval dealt to the entity every interval
    /// </summary>
    [DataField, AutoNetworkedField]
    public DamageSpecifier Damage = new();

    /// <summary>
    /// Minimum bleed amount it takes for blood level to stop decreasing while healing / damaging.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MinimumBleedAmount = 2.0f;

    /// <summary>
    /// Below this percentage of blood level, the blood level will no longer decrease passively while healing / damaging.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float MaximumBloodPercentage = 0.2f;

    /// <summary>
    /// Amount of blood level to decrease every interval.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 BloodLevelDecrease = 0.1;

    /// <summary>
    /// Accumulative multiplier to decrease damage/healing as blood level drops.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BloodLevelDamageMultiplier = 0.9f;

    /// <summary>
    /// Delay between damage events.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan Interval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The maximum HP the damage will be given to. If 0, disabled.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 MaximumDamage = 0;

    /// <summary>
    /// The minimum damage for the healing / damage to apply. Set to zero if you don't want this.
    /// </summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 MinimumDamage = 0;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextDamage = TimeSpan.Zero;
}
