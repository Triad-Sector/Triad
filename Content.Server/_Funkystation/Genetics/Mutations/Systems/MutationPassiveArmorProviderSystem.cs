using Content.Server._Funkystation.Genetics.Mutations.Components;
using Content.Shared.Damage;
using Robust.Shared.Prototypes;

namespace Content.Server._Funkystation.Genetics.Mutations.Systems;

public sealed class MutationPassiveArmorProviderSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MutationPassiveArmorProviderComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnDamageModify(EntityUid uid, MutationPassiveArmorProviderComponent comp, ref DamageModifyEvent args)
    {
        if (comp.ModifierSetId == default)
            return;

        if (!_proto.TryIndex(comp.ModifierSetId, out var modifiers))
            return;

        args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modifiers);
    }
}
