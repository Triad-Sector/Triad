using Content.Shared.FixedPoint;
using Content.Shared._Goobstation.Factory;
using Content.Shared._Goobstation.Factory.Slots;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Timing;

namespace Content.Shared._Goobstation.Factory.Plumbing;

public sealed class PlumbingPumpSystem : EntitySystem
{
    [Dependency] private readonly ExclusiveSlotsSystem _exclusive = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PlumbingFilterSystem _filter = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    private EntityQuery<SolutionTransferComponent> _transferQuery;

    public override void Initialize()
    {
        base.Initialize();
        _transferQuery = GetEntityQuery<SolutionTransferComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<PlumbingPumpComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextUpdate)
                continue;

            comp.NextUpdate = now + comp.UpdateDelay;
            TryPump((uid, comp));
        }
    }

    private void TryPump(Entity<PlumbingPumpComponent> ent)
    {
        var owner = ent.Owner;

        if (!_power.IsPowered(owner))
        {
            return;
        }

        // pump does nothing unless both slots are linked
        var inSlot = _exclusive.GetInputSlot(owner);
        var outSlot = _exclusive.GetOutputSlot(owner);

        if (inSlot == null)
        {
            // fallback: try re-resolving the exclusive input slot component (handles deserialization ordering)
            if (TryComp(owner, out ExclusiveInputSlotComponent? inComp))
            {
                _exclusive.UpdateSlot(inComp);
                inSlot = _exclusive.GetInputSlot(owner);
            }

            if (inSlot == null)
            {
                return;
            }
        }

        if (outSlot == null)
        {
            // fallback: try re-resolving the exclusive output slot component (handles deserialization ordering)
            if (TryComp(owner, out ExclusiveOutputSlotComponent? outComp))
            {
                _exclusive.UpdateSlot(outComp);
                outSlot = _exclusive.GetOutputSlot(owner);
            }

            if (outSlot == null)
            {
                return;
            }
        }

        Entity<SolutionComponent> inputEnt;
        Entity<SolutionComponent> outputEnt;
        try
        {
            var maybeIn = inSlot.GetSolution();
            if (maybeIn is not {} ie)
            {
                return;
            }
            inputEnt = ie;
        }
        catch (Exception)
        {
            return;
        }

        try
        {
            var maybeOut = outSlot.GetSolution();
            if (maybeOut is not {} oe)
            {
                return;
            }
            outputEnt = oe;
        }
        catch (Exception)
        {
            return;
        }

        var input = inputEnt.Comp.Solution;
        var output = outputEnt.Comp.Solution;

        var limit = _transferQuery.Comp(ent).TransferAmount;
        

        var amount = FixedPoint2.Min(input.Volume, limit);
        if (output.MaxVolume > FixedPoint2.Zero)
            amount = FixedPoint2.Min(amount, output.AvailableVolume);

        

        if (amount <= FixedPoint2.Zero)
        {
            return;
        }

        var filter = _filter.GetFilteredReagent(ent);
        

        var split = filter is {} f
            ? input.SplitSolutionWithOnly(amount, f)
            : input.SplitSolution(amount);

        _solution.UpdateChemicals(inputEnt, false); // removing reagents should never cause reactions? don't waste cpu updating it
        _solution.ForceAddSolution(outputEnt, split);

        
    }
}
