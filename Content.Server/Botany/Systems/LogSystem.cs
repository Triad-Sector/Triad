using Content.Server.Botany.Components;
using Content.Server.Kitchen.Components;
using Content.Server.Stack;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Random;
using Content.Shared.Stacks;
using Robust.Shared.Containers;

namespace Content.Server.Botany.Systems;

public sealed class LogSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly RandomHelperSystem _randomHelper = default!;
    [Dependency] private readonly StackSystem _stackSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LogComponent, InteractUsingEvent>(OnInteractUsing);
    }

    private void OnInteractUsing(EntityUid uid, LogComponent component, InteractUsingEvent args)
    {
        if (!HasComp<SharpComponent>(args.Used))
            return;

        var stackCount = 1;
        if (TryComp<StackComponent>(uid, out var stack))
            stackCount = stack.Count;

        // if in some container, try pick up, else just drop to world
        var inContainer = _containerSystem.IsEntityInContainer(uid);
        var pos = Transform(uid).Coordinates;

        for (var i = 0; i < component.SpawnCount; i++)
        {
            var plank = Spawn(component.SpawnedPrototype, pos);

            if (inContainer)
                _handsSystem.PickupOrDrop(args.User, plank);
            else
            {
                var xform = Transform(plank);
                _containerSystem.AttachParentToContainerOrGrid((plank, xform));
                xform.LocalRotation = 0;
                _randomHelper.RandomOffset(plank, 0.25f);
            }
        }

        if (stackCount > 1 && stack != null)
            _stackSystem.SetCount(uid, stack.Count - 1, stack);
        else
            QueueDel(uid);

        args.Handled = true;
    }
}
