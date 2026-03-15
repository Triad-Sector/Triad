using Content.Shared.ActionBlocker;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Events;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared.CM14.Xenos.Rest;

public sealed class XenoRestSystem : EntitySystem
{
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<XenoComponent, XenoRestActionEvent>(OnXenoRest);
        SubscribeLocalEvent<XenoRestingComponent, UpdateCanMoveEvent>(OnXenoRestingCanMove);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<XenoComponent, XenoRestingComponent, DamageableComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var xeno, out var resting, out var damage, out var mob))
        {
            // throttle to once per second
            if (resting.NextTick + TimeSpan.FromSeconds(1) > curTime)
                continue;

            resting.NextTick = curTime;

            if (_mobState.IsDead(uid, mob))
                continue;

            var regen = xeno.RestHealing;
            if (regen == null)
                continue;

            if (_mobState.IsCritical(uid, mob))
                regen = regen * xeno.RestHealingCritMultiplier;

            _damageable.TryChangeDamage(uid, regen, true, false, damage);
        }
    }

    private void OnXenoRestingCanMove(Entity<XenoRestingComponent> ent, ref UpdateCanMoveEvent args)
    {
        args.Cancel();
    }

    private void OnXenoRest(Entity<XenoComponent> ent, ref XenoRestActionEvent args)
    {
        if (_timing.ApplyingState)
            return;

        if (HasComp<XenoRestingComponent>(ent))
        {
            RemComp<XenoRestingComponent>(ent);
            _appearance.SetData(ent, XenoVisualLayers.Base, XenoRestState.NotResting);
        }
        else
        {
            AddComp<XenoRestingComponent>(ent);
            _appearance.SetData(ent, XenoVisualLayers.Base, XenoRestState.Resting);
        }

        _actionBlocker.UpdateCanMove(ent);
    }
}
