using Content.Server.Emp;
using Content.Server.Ghost;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.Random; // Frontier
using System; // HardLight
using Content.Shared.Containers; // HardLight

namespace Content.Server.Light.EntitySystems;

/// <summary>
///     System for the PoweredLightComponents
/// </summary>
public sealed class PoweredLightSystem : SharedPoweredLightSystem
{
    [Dependency] private readonly IRobustRandom _random = default!; // Frontier

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PoweredLightComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<PoweredLightComponent, GhostBooEvent>(OnGhostBoo);

        SubscribeLocalEvent<PoweredLightComponent, EmpPulseEvent>(OnEmpPulse);
    }

    private void OnGhostBoo(EntityUid uid, PoweredLightComponent light, GhostBooEvent args)
    {
        if (light.IgnoreGhostsBoo)
            return;

        // check cooldown first to prevent abuse
        var time = GameTiming.CurTime;
        if (light.LastGhostBlink != null)
        {
            if (time <= light.LastGhostBlink + light.GhostBlinkingCooldown)
                return;
        }

        light.LastGhostBlink = time;

        ToggleBlinkingLight(uid, light, true);
        uid.SpawnTimer(light.GhostBlinkingTime, () =>
        {
            ToggleBlinkingLight(uid, light, false);
        });

        args.Handled = true;
    }

    // HardLight start
    private void OnMapInit(EntityUid uid, PoweredLightComponent light, MapInitEvent args)
    {
        UpdateLight(uid, light);

        // If the light doesn't have a bulb and isn't set to be filled with one, make sure it's off.
        if (GetBulb(uid, light) == null && !HasComp<ContainerFillComponent>(uid))
            return;

        ScheduleLateRefresh(uid);
    }

    // Schedules a light update for a later time to ensure bulbs are properly inserted before checking for them.
    private void ScheduleLateRefresh(EntityUid uid)
    {
        uid.SpawnTimer(TimeSpan.FromSeconds(0.05), () =>
        {
            if (TerminatingOrDeleted(uid))
                return;

            if (!TryComp<PoweredLightComponent>(uid, out var light))
                return;

            var bulb = GetBulb(uid, light);
            if (bulb != null)
            {
                if (ContainerSystem.Remove(bulb.Value, light.LightBulbContainer))
                    ContainerSystem.Insert(bulb.Value, light.LightBulbContainer);
            }

            UpdateLight(uid, light);
        });
    }
    // HardLight end

    private void OnEmpPulse(EntityUid uid, PoweredLightComponent component, ref EmpPulseEvent args)
    {
        // Frontier start: break lights probabilistically
        if (_random.Prob(component.LightBreakChance))
        {
            if (TryDestroyBulb(uid, component))
                args.Affected = true;
        }
        // Frontier end
    }
}
