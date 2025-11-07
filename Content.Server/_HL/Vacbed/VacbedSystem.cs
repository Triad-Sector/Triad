using Content.Server.Fluids.EntitySystems;
using Content.Shared._HL.Vacbed;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Climbing.Systems;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Verbs;
using Robust.Shared.GameObjects;
using Content.Shared.Chemistry.Components;


namespace Content.Server._HL.Vacbed;

public sealed partial class VacbedSystem : SharedVacbedSystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly ClimbSystem _climbSystem = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private readonly PuddleSystem _puddleSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VacbedComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<VacbedComponent, GetVerbsEvent<AlternativeVerb>>(AddAlternativeVerbs);
        SubscribeLocalEvent<VacbedComponent, VacbedDragFinished>(OnDragFinished);
        SubscribeLocalEvent<VacbedComponent, DragDropTargetEvent>(HandleDragDropOn);
    }

    private void HandleDragDropOn(Entity<VacbedComponent> entity, ref DragDropTargetEvent args)
    {
        if (entity.Comp.BodyContainer.ContainedEntity != null)
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, entity.Comp.EntryDelay, new VacbedDragFinished(), entity, target: args.Dragged, used: entity)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = false,
        };
        _doAfterSystem.TryStartDoAfter(doAfterArgs);
        args.Handled = true;
    }

    private void OnDragFinished(Entity<VacbedComponent> entity, ref VacbedDragFinished args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target == null)
            return;

        if (InsertBody(entity.Owner, args.Args.Target.Value, entity.Comp))
        {
            //todo adminlog
        }
        args.Handled = true;
    }

    public override EntityUid? EjectBody(EntityUid uid, VacbedComponent? vacbedComponent)
    {
        if (!Resolve(uid, ref vacbedComponent))
            return null;
        if (vacbedComponent.BodyContainer.ContainedEntity is not { Valid: true } contained)
            return null;
        base.EjectBody(uid, vacbedComponent);
        _climbSystem.ForciblySetClimbing(contained, uid);

        //I AM A GOD IN HUMAN CLOTHING AND I MADE THIS WORK
        if (_solutionContainerSystem.TryGetDrainableSolution(vacbedComponent.Owner, out var soln, out var solution) /*&& solution.Volume != 0*/)
        {
            //for the love of fuck execute
            var puddleSolution = _solutionContainerSystem.SplitSolution(soln.Value, solution.Volume);
            _puddleSystem.TrySpillAt(Transform(uid).Coordinates, puddleSolution, out _);
        }
        //as god is my witness i will get my revenge on whoever did the solutions api stuff
        //i wasted a whole day making this work
        //the commnet inside the if statement stays, it's load bearing to my mental health

        return contained;
    }
}
