using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.Standing;
using Robust.Shared.Containers;

namespace Content.Shared._HL.Vacbed;

public abstract partial class SharedVacbedSystem
{
    [Dependency] private readonly BlindableSystem _blindableSystem = default!;

    public virtual void InitializeInsideVacbed()
    {
        SubscribeLocalEvent<InsideVacbedComponent, DownAttemptEvent>(HandleDown);
        SubscribeLocalEvent<InsideVacbedComponent, EntGotRemovedFromContainerMessage>(OnEntGotRemovedFromContainer);
        SubscribeLocalEvent<InsideVacbedComponent, ComponentInit>(InsideVacbedInit);
        SubscribeLocalEvent<InsideVacbedComponent, CanSeeAttemptEvent>(OnVacbedTrySee);
    }

    private void HandleDown(EntityUid uid, InsideVacbedComponent component, DownAttemptEvent args)
    {
        args.Cancel(); //keeps person inside standing
    }

    //should be private but has to be public so i can override it in server system
    public virtual void InsideVacbedInit(EntityUid uid, InsideVacbedComponent insideVacbedComponent, ComponentInit args)
    {
        _blindableSystem.UpdateIsBlind(insideVacbedComponent.Owner);
    }

    private void OnVacbedTrySee(EntityUid uid, InsideVacbedComponent insideVacbedComponent, CanSeeAttemptEvent args)
    {
        args.Cancel();
    }

    //should be private but has to be public so i can override it in server system
    public virtual void OnEntGotRemovedFromContainer(EntityUid uid, InsideVacbedComponent component, EntGotRemovedFromContainerMessage args)
    {
        if (Terminating(uid))
        {
            return;
        }

        RemComp<InsideVacbedComponent>(uid);
        _blindableSystem.UpdateIsBlind(component.Owner);
    }

}
