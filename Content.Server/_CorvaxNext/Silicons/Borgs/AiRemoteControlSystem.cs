using Content.Server.Radio.Components;
using Content.Server.Silicons.Laws;
using Content.Shared._CorvaxNext.Silicons.Borgs;
using Content.Shared._CorvaxNext.Silicons.Borgs.Components;
using Content.Shared.Actions;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Standing;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.StationAi;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Content.Shared.Body.Part;
using Robust.Shared.Containers;
using Robust.Shared.Player;

namespace Content.Server._CorvaxNext.Silicons.Borgs;

public sealed class AiRemoteControlSystem : SharedAiRemoteControlSystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SiliconLawSystem _lawSystem = default!;
    [Dependency] private readonly SharedStationAiSystem _stationAiSystem = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly SharedTransformSystem _xformSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AiRemoteControllerComponent, ReturnMindIntoAiEvent>(OnReturnMindIntoAi);
        SubscribeLocalEvent<AiRemoteControllerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AiRemoteControllerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<AiRemoteControllerComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<StationAiHeldComponent, AiRemoteControllerComponent.RemoteDeviceActionMessage>(OnUiRemoteAction);
        SubscribeLocalEvent<StationAiHeldComponent, ToggleRemoteDevicesScreenEvent>(OnToggleRemoteDevicesScreen);

        SubscribeLocalEvent<AiRemoteBrainComponent, EntGotInsertedIntoContainerMessage>(OnBrainInserted);
        SubscribeLocalEvent<AiRemoteBrainComponent, EntGotRemovedFromContainerMessage>(OnBrainRemoved);
    }

    private void OnBrainInserted(EntityUid uid, AiRemoteBrainComponent component, EntGotInsertedIntoContainerMessage args)
    {
        var target = GetRemoteTarget(args.Container.Owner);
        if (target != null)
            EnsureComp<AiRemoteControllerComponent>(target.Value);
    }

    private void OnBrainRemoved(EntityUid uid, AiRemoteBrainComponent component, EntGotRemovedFromContainerMessage args)
    {
        var target = GetRemoteTarget(args.Container.Owner);
        if (target != null)
            RemComp<AiRemoteControllerComponent>(target.Value);
    }

    private EntityUid? GetRemoteTarget(EntityUid containerOwner)
    {
        if (HasComp<BorgChassisComponent>(containerOwner))
            return containerOwner;

        if (TryComp<BodyPartComponent>(containerOwner, out var bodyPart) && bodyPart.Body != null)
            return bodyPart.Body.Value;

        return null;
    }

    private void OnMapInit(Entity<AiRemoteControllerComponent> entity, ref MapInitEvent args)
    {
        var visionComp = EnsureComp<StationAiVisionComponent>(entity.Owner);
        EntityUid? actionEnt = null;

        _actions.AddAction(entity.Owner, ref actionEnt, entity.Comp.BackToAiAction);

        if (actionEnt != null)
            entity.Comp.BackToAiActionEntity = actionEnt.Value;
    }

    private void OnShutdown(Entity<AiRemoteControllerComponent> entity, ref ComponentShutdown args)
    {
        _actions.RemoveAction(entity.Owner, entity.Comp.BackToAiActionEntity);

        var backArgs = new ReturnMindIntoAiEvent();
        backArgs.Performer = entity;

        if (TryComp(entity, out IntrinsicRadioTransmitterComponent? transmitter)
            && entity.Comp.PreviouslyTransmitterChannels != null)
            transmitter.Channels = [.. entity.Comp.PreviouslyTransmitterChannels];

        if (TryComp(entity, out ActiveRadioComponent? activeRadio)
            && entity.Comp.PreviouslyActiveRadioChannels != null)
            activeRadio.Channels = [.. entity.Comp.PreviouslyActiveRadioChannels];

        ReturnMindIntoAi(entity);
    }

    private void OnGetVerbs(Entity<AiRemoteControllerComponent> entity, ref GetVerbsEvent<AlternativeVerb> args)
    {
        var user = args.User;

        if (!TryComp<StationAiHeldComponent>(user, out var stationAiHeldComp))
            return;

        var verb = new AlternativeVerb
        {
            Text = Loc.GetString("ai-remote-control"),
            Act = () => AiTakeControl(user, entity)
        };
        args.Verbs.Add(verb);
    }

    private void OnReturnMindIntoAi(Entity<AiRemoteControllerComponent> entity, ref ReturnMindIntoAiEvent args) =>
        ReturnMindIntoAi(entity);

    public void AiTakeControl(EntityUid ai, EntityUid entity)
    {
        if (!_mind.TryGetMind(ai, out var mindId, out var mind))
            return;

        if (!TryComp<StationAiHeldComponent>(ai, out var stationAiHeldComp))
            return;

        if (!TryComp<AiRemoteControllerComponent>(entity, out var aiRemoteComp))
            return;

        if (TryComp(entity, out IntrinsicRadioTransmitterComponent? transmitter))
        {
            aiRemoteComp.PreviouslyTransmitterChannels = [.. transmitter.Channels];

            if (TryComp(ai, out IntrinsicRadioTransmitterComponent? stationAiTransmitter))
                transmitter.Channels = [.. stationAiTransmitter.Channels];
        }

        if (TryComp(entity, out ActiveRadioComponent? activeRadio))
        {
            aiRemoteComp.PreviouslyActiveRadioChannels = [.. activeRadio.Channels];

            if (TryComp(ai, out ActiveRadioComponent? stationAiActiveRadio))
                activeRadio.Channels = [.. stationAiActiveRadio.Channels];
        }

        _mind.ControlMob(ai, entity);
        aiRemoteComp.AiHolder = ai;
        aiRemoteComp.LinkedMind = mindId;

        if (TryComp<MobStateComponent>(entity, out var mobState) && _mobState.IsDead(entity, mobState))
        {
            if (_mobThreshold.TryGetThresholdForState(entity, MobState.Critical, out var critThreshold)
                && TryComp<DamageableComponent>(entity, out var damageable)
                && damageable.TotalDamage < critThreshold)
            {
                _mobState.ChangeMobState(entity, MobState.Alive, mobState);
            }
            else
                return;
        }

        _standing.Stand(entity, force: true);

        stationAiHeldComp.CurrentConnectedEntity = entity;

        if (!_stationAiSystem.TryGetCore(ai, out var stationAiCore))
            return;

        _stationAiSystem.SwitchRemoteEntityMode(stationAiCore, false);

        RewriteLaws(ai, entity);
    }

    private void OnToggleRemoteDevicesScreen(EntityUid uid, StationAiHeldComponent component, ToggleRemoteDevicesScreenEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(uid, out var actor))
            return;
        args.Handled = true;

        _userInterface.TryToggleUi(uid, RemoteDeviceUiKey.Key, actor.PlayerSession);

        var query = EntityManager.EntityQueryEnumerator<AiRemoteControllerComponent>();
        var remoteDevices = new List<RemoteDevicesData>();

        while (query.MoveNext(out var queryUid, out var comp))
        {
            var data = new RemoteDevicesData
            {
                NetEntityUid = GetNetEntity(queryUid),
                DisplayName = Comp<MetaDataComponent>(queryUid).EntityName
            };

            remoteDevices.Add(data);
        }

        var state = new RemoteDevicesBuiState(remoteDevices);
        _userInterface.SetUiState(uid, RemoteDeviceUiKey.Key, state);
    }

    private void OnUiRemoteAction(EntityUid uid, StationAiHeldComponent component, AiRemoteControllerComponent.RemoteDeviceActionMessage msg)
    {
        if (msg.RemoteAction == null)
            return;

        var target = GetEntity(msg.RemoteAction?.Target);

        if (!HasComp<AiRemoteControllerComponent>(target))
            return;

        switch (msg.RemoteAction?.ActionType)
        {
            case RemoteDeviceActionEvent.RemoteDeviceActionType.MoveToDevice:
                if (!_stationAiSystem.TryGetCore(uid, out var stationAiCore)
                    || stationAiCore.Comp?.RemoteEntity == null)
                    return;
                _xformSystem.SetCoordinates(stationAiCore.Comp.RemoteEntity.Value, Transform(target.Value).Coordinates);
                break;

            case RemoteDeviceActionEvent.RemoteDeviceActionType.TakeControl:
                AiTakeControl(uid, target.Value);
                break;
        }
    }

    private void RewriteLaws(EntityUid from, EntityUid to)
    {
        if (!TryComp<SiliconLawProviderComponent>(from, out var fromLawsComp))
            return;

        if (!TryComp<SiliconLawProviderComponent>(to, out var toLawsComp))
            return;

        if (fromLawsComp.Lawset == null)
            return;

        var fromLaws = _lawSystem.GetLaws(from);
        _lawSystem.SetLawsSilent(fromLaws.Laws, to);
    }
}
