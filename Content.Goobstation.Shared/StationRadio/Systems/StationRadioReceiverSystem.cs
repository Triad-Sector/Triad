using Content.Goobstation.Shared.StationRadio.Components;
using Content.Goobstation.Shared.StationRadio.Events;
using Content.Shared.Interaction;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Goobstation.Shared.StationRadio.Systems;

public sealed class StationRadioReceiverSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationRadioReceiverComponent, StationRadioMediaPlayedEvent>(OnMediaPlayed);
        SubscribeLocalEvent<StationRadioReceiverComponent, StationRadioMediaStoppedEvent>(OnMediaStopped);
        SubscribeLocalEvent<StationRadioReceiverComponent, ActivateInWorldEvent>(OnRadioToggle);
        SubscribeLocalEvent<StationRadioReceiverComponent, PowerChangedEvent>(OnPowerChanged);
    }

    private void OnPowerChanged(EntityUid uid, StationRadioReceiverComponent comp, PowerChangedEvent args)
    {
        TrySetGain(uid, comp, args.Powered && comp.Active ? 1f : 0f);
    }

    private void OnRadioToggle(EntityUid uid, StationRadioReceiverComponent comp, ActivateInWorldEvent args)
    {
        comp.Active = !comp.Active;
        TrySetGain(uid, comp, _power.IsPowered(uid) && comp.Active ? 1f : 0f);
    }

    private void TrySetGain(EntityUid uid, StationRadioReceiverComponent comp, float gain)
    {
        if (comp.SoundEntity is not { } sound)
            return;

        if (!sound.Valid || TerminatingOrDeleted(sound) || !HasComp<AudioComponent>(sound))
        {
            comp.SoundEntity = null;
            Dirty(uid, comp);
            return;
        }

        _audio.SetGain(sound, gain);
    }

    private void OnMediaPlayed(EntityUid uid, StationRadioReceiverComponent comp, StationRadioMediaPlayedEvent args)
    {
        var gain = comp.Active ? 3f : 0f;
        var audio = _audio.PlayPredicted(args.MediaPlayed, uid, uid, AudioParams.Default.WithVolume(3f).WithMaxDistance(8.5f));
        if (audio != null && _power.IsPowered(uid))
        {
            comp.SoundEntity = audio.Value.Entity;
            _audio.SetGain(comp.SoundEntity, gain);
        }
        else if(audio != null && !_power.IsPowered(uid))
        {
            comp.SoundEntity = audio.Value.Entity;
            _audio.SetGain(comp.SoundEntity, 0);
        }
    }

    private void OnMediaStopped(EntityUid uid, StationRadioReceiverComponent comp, StationRadioMediaStoppedEvent args)
    {
        if (comp.SoundEntity != null)
            comp.SoundEntity = _audio.Stop(comp.SoundEntity);
    }
}
