using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Content.Server.Botany.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.FixedPoint;
using Content.Shared.Stacks;
using Robust.Shared.Utility;

namespace Content.Server.Botany.Systems;

public sealed partial class BotanySystem
{
    private void OnSeedStartup(EntityUid uid, SeedComponent component, ComponentStartup args)
    {
        UpdateSeedStackSignature(uid, component);
    }

    private void OnProduceStartup(EntityUid uid, ProduceComponent component, ComponentStartup args)
    {
        UpdateProduceStackSignature(uid, component, null);
    }

    private void OnProduceSolutionChanged(EntityUid uid, ProduceComponent component, ref SolutionContainerChangedEvent args)
    {
        if (!string.Equals(args.SolutionId, component.SolutionName, StringComparison.Ordinal))
            return;

        UpdateProduceStackSignature(uid, component, args.Solution);
    }

    private void UpdateSeedStackSignature(EntityUid uid, SeedComponent component)
    {
        if (!TryGetSeed(component, out var seed))
            return;

        var signature = BuildSeedChemicalSignature(uid, seed);
        var stackSig = EnsureComp<StackSignatureComponent>(uid);
        if (stackSig.Signature == signature)
            return;

        stackSig.Signature = signature;
        Dirty(uid, stackSig);
    }

    private void UpdateProduceStackSignature(EntityUid uid, ProduceComponent component, Solution? solution)
    {
        var signature = BuildProduceSignature(uid, component, solution);
        if (signature == null)
            return;

        var stackSig = EnsureComp<StackSignatureComponent>(uid);
        if (stackSig.Signature == signature)
            return;

        stackSig.Signature = signature;
        Dirty(uid, stackSig);
    }

    private string? BuildProduceSignature(EntityUid uid, ProduceComponent component, Solution? solution)
    {
        if (solution == null)
        {
            if (!TryComp(uid, out SolutionContainerManagerComponent? manager))
                return null;

            if (!_solutionContainerSystem.TryGetSolution((uid, manager), component.SolutionName, out _, out var found))
                return null;

            solution = found;
        }

        var builder = new StringBuilder();
        AppendPrototypeId(builder, uid);
        AppendSolutionSignature(builder, solution);
        if (TryGetSeed(component, out var seed))
            builder.Append("seed=").Append(BuildSeedChemicalSignature(uid, seed));
        return builder.ToString();
    }

    private string BuildSeedChemicalSignature(EntityUid uid, SeedData seed)
    {
        var builder = new StringBuilder();
        AppendPrototypeId(builder, uid);

        builder.Append("pot=")
            .Append(seed.Potency.ToString(CultureInfo.InvariantCulture))
            .Append(';');
        builder.Append("yield=").Append(seed.Yield).Append(';');
        builder.Append("endur=")
            .Append(seed.Endurance.ToString(CultureInfo.InvariantCulture))
            .Append(';');
        builder.Append("life=")
            .Append(seed.Lifespan.ToString(CultureInfo.InvariantCulture))
            .Append(';');
        builder.Append("mature=")
            .Append(seed.Maturation.ToString(CultureInfo.InvariantCulture))
            .Append(';');
        builder.Append("prod=")
            .Append(seed.Production.ToString(CultureInfo.InvariantCulture))
            .Append(';');
        builder.Append("stages=").Append(seed.GrowthStages).Append(';');
        builder.Append("harvest=").Append((int) seed.HarvestRepeat).Append(';');
        builder.Append("seedless=").Append(seed.Seedless ? '1' : '0').Append(';');
        builder.Append("viable=").Append(seed.Viable ? '1' : '0').Append(';');
        builder.Append("ligneous=").Append(seed.Ligneous ? '1' : '0').Append(';');

        foreach (var (chem, quantity) in seed.Chemicals.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            var amount = FixedPoint2.New(quantity.Min);
            if (quantity.PotencyDivisor > 0 && seed.Potency > 0)
                amount += FixedPoint2.New(seed.Potency / quantity.PotencyDivisor);
            amount = FixedPoint2.New(MathHelper.Clamp(amount.Float(), quantity.Min, quantity.Max));
            builder.Append(chem).Append('=').Append(amount.Value).Append(';');
        }

        return builder.ToString();
    }

    private void AppendPrototypeId(StringBuilder builder, EntityUid uid)
    {
        var prototypeId = Prototype(uid)?.ID;
        if (prototypeId != null)
            builder.Append(prototypeId).Append('|');
    }

    private static void AppendSolutionSignature(StringBuilder builder, Solution solution)
    {
        foreach (var reagent in solution.Contents.OrderBy(r => r.Reagent.Prototype, StringComparer.Ordinal))
        {
            builder.Append(reagent.Reagent.Prototype)
                .Append('=')
                .Append(reagent.Quantity.Value)
                .Append(';');
        }
    }
}
