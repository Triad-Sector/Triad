using Content.Server.Administration.Logs;
using Content.Server.Nuke;
using Content.Server.Popups;
using Content.Server._NF.Bank;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Mind;
using Content.Shared.NukeOps;
using Content.Shared.Popups;
using Content.Shared.Database;

namespace Content.Server.NukeOps;

/// <summary>
/// Pays nuke operatives when a nuclear device successfully detonates.
/// This is separate from objective rewards because detonation is tracked as a global event.
/// </summary>
public sealed class NukeDetonationRewardSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;

    // TODO: Make configurable via CVars or a prototype if needed.
    private const int RewardAmount = 2500000;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<NukeExplodedEvent>(OnNukeExploded);
    }

    private void OnNukeExploded(NukeExplodedEvent ev)
    {
        // Iterate all operatives and pay them.
        var ops = EntityQuery<NukeOperativeComponent>();
        foreach (var op in ops)
        {
            if (!_mind.TryGetMind(op.Owner, out var mindId, out var mind))
                continue;

            if (mind.OwnedEntity is not { } mob || !HasComp<BankAccountComponent>(mob))
                continue;

            if (_bank.TryBankDeposit(mob, RewardAmount))
            {
                _popup.PopupEntity($"The nuke detonated! You were paid {Content.Shared._NF.Bank.BankSystemExtensions.ToSpesoString(RewardAmount)}.", mob, mob);
                _adminLog.Add(LogType.Action, LogImpact.Medium, $"NukeDetonationReward: Paid {RewardAmount} to {_mind.MindOwnerLoggingString(mind)} for nuclear detonation.");
            }
        }
    }
}
