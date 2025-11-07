using Content.Server.Administration.Logs;
using Content.Server.Popups;
using Content.Server._NF.Bank;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Database;
using Content.Shared.Mind;
using Content.Shared.Zombies;
using Content.Shared.Popups;
using Robust.Shared.Player;

namespace Content.Server.Zombies;

/// <summary>
/// Pays zombies when someone they have infected finishes converting into a zombie.
/// Rewards are granted to the most recent infector recorded on the infected target.
/// </summary>
public sealed class ZombieConversionRewardSystem : EntitySystem
{
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IAdminLogManager _adminLog = default!;

    // TODO: Make configurable via CVars or prototype if desired.
    private const int RewardAmount = 20_000;

    // Track targets we've already rewarded for to avoid duplicate payouts (e.g., multiple listeners).
    private readonly HashSet<EntityUid> _rewardedTargets = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EntityZombifiedEvent>(OnEntityZombified);
    }

    private void OnEntityZombified(ref EntityZombifiedEvent ev)
    {
        var target = ev.Target;
        if (_rewardedTargets.Contains(target))
            return;

        if (!TryComp<PendingZombieComponent>(target, out var pending) || pending.Infector is not { } infector)
            return;

        // Try to pay the infector's current body first if it has a bank account, otherwise fall back to their owned entity via mind.
        if (!TryPayInfector(infector, out var paidEntity))
            return;

        _rewardedTargets.Add(target);

        // Notify the player if applicable
        var msg = $"You converted {ToPrettyString(target)}. You were paid {Content.Shared._NF.Bank.BankSystemExtensions.ToSpesoString(RewardAmount)}.";
        _popup.PopupEntity(msg, paidEntity, Filter.Entities(paidEntity), false, PopupType.Small);

        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"ZombieConversionReward: Paid {RewardAmount} to {ToPrettyString(paidEntity)} for zombifying {ToPrettyString(target)} (infector: {ToPrettyString(infector)}).");
    }

    private bool TryPayInfector(EntityUid infector, out EntityUid paidEntity)
    {
        paidEntity = EntityUid.Invalid;

        // If the infector entity itself has a bank account, pay it directly.
        if (HasComp<BankAccountComponent>(infector))
        {
            if (_bank.TryBankDeposit(infector, RewardAmount))
            {
                paidEntity = infector;
                return true;
            }
            return false;
        }

        // Otherwise try paying their currently owned entity via mind.
        if (_mind.TryGetMind(infector, out var mindId, out var mind) && mind.OwnedEntity is { } owned && HasComp<BankAccountComponent>(owned))
        {
            if (_bank.TryBankDeposit(owned, RewardAmount))
            {
                paidEntity = owned;
                return true;
            }
        }

        return false;
    }
}
