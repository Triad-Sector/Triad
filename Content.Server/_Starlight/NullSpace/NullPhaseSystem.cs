using Content.Shared.Inventory.Events;
using Content.Shared.Clothing.Components;
using Content.Shared.Actions;
using Content.Shared._Starlight.NullSpace;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Content.Shared.Popups;
using Content.Shared.Physics;
using Content.Shared._Starlight;
using System.Linq;
using Content.Server.Ghost;
using Robust.Server.Containers;
using Robust.Shared.Prototypes;
using Content.Shared.Light.Components; // HardLight: Merged with upstream

namespace Content.Server._Starlight.NullSpace;

public sealed class EtherealPhaseSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly GhostSystem _ghost = default!;
    [Dependency] private readonly ContainerSystem _container = default!;

    private EntProtoId ShadekinShadow = "ShadekinShadow";
    private EntProtoId ShadekinPhaseInEffect = "ShadekinPhaseInEffect";
    private EntProtoId ShadekinPhaseOutEffect = "ShadekinPhaseOutEffect";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NullPhaseComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<NullPhaseComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<NullPhaseComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<NullPhaseComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<NullPhaseComponent, NullPhaseActionEvent>(OnPhaseAction);
    }

    private void OnStartup(EntityUid uid, NullPhaseComponent component, ComponentStartup args)
    {
        Toggle(uid, component, true);
    }

    public void OnShutdown(EntityUid uid, NullPhaseComponent component, ComponentShutdown args)
    {
        Toggle(uid, component, false);
    }

    private void OnEquipped(EntityUid uid, NullPhaseComponent component, GotEquippedEvent args)
    {
        if (!TryComp<ClothingComponent>(uid, out var clothing)
            || !clothing.Slots.HasFlag(args.SlotFlags))
            return;

        EnsureComp<NullPhaseComponent>(args.Equipee);
    }

    private void OnUnequipped(EntityUid uid, NullPhaseComponent component, GotUnequippedEvent args)
    {
        RemComp<NullPhaseComponent>(args.Equipee);
    }

    private void OnPhaseAction(EntityUid uid, NullPhaseComponent component, NullPhaseActionEvent args)
    {
        // Perform phase on the user performing the action, not the provider entity.
        Phase(args.Performer);
        args.Handled = true;
    }

    private void Toggle(EntityUid uid, NullPhaseComponent component, bool toggle)
    {
        if (toggle)
            _actionsSystem.AddAction(uid, ref component.PhaseAction, "NullPhaseAction", uid);
        else
            _actionsSystem.RemoveAction(uid, component.PhaseAction);
    }

    public bool Phase(EntityUid uid)
    {
        if (TryComp<NullSpaceComponent>(uid, out var ethereal))
        {
            if (HasComp<ShadekinComponent>(uid))
            {
                var lightQuery = _lookup.GetEntitiesInRange(uid, 5, flags: LookupFlags.StaticSundries)
                    .Where(x => HasComp<PoweredLightComponent>(x));
                foreach (var light in lightQuery)
                    _ghost.DoGhostBooEvent(light);

                var effect = SpawnAtPosition(ShadekinPhaseInEffect, Transform(uid).Coordinates);
                Transform(effect).LocalRotation = Transform(uid).LocalRotation;
            }
            else
                SpawnAtPosition(ShadekinShadow, Transform(uid).Coordinates);

            RemComp<NullSpaceComponent>(uid);
        }
        else
        {
            if (_container.IsEntityInContainer(uid))
            {
                _popup.PopupEntity(Loc.GetString("phase-fail-generic"), uid, uid);
                return false;
            }

            EnsureComp<NullSpaceComponent>(uid);

            if (HasComp<ShadekinComponent>(uid))
            {
                var lightQuery = _lookup.GetEntitiesInRange(uid, 5, flags: LookupFlags.StaticSundries)
                    .Where(x => HasComp<PoweredLightComponent>(x));
                foreach (var light in lightQuery)
                    _ghost.DoGhostBooEvent(light);

                var effect = SpawnAtPosition(ShadekinPhaseOutEffect, Transform(uid).Coordinates);
                Transform(effect).LocalRotation = Transform(uid).LocalRotation;
            }
            else
                SpawnAtPosition(ShadekinShadow, Transform(uid).Coordinates);
        }
        return true;
    }
}
