using Content.Client.Medical.Cryogenics;
using Content.Shared._HL.Vacbed;
using Content.Shared.Medical.Cryogenics;
using Content.Shared.Verbs;
using Robust.Client.GameObjects;
using System.Numerics;
using DrawDepth = Content.Shared.DrawDepth.DrawDepth;

namespace Content.Client._HL.Vacbed;

public sealed class VacbedSystem : SharedVacbedSystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<VacbedComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<VacbedComponent, GetVerbsEvent<AlternativeVerb>>(AddAlternativeVerbs);
        SubscribeLocalEvent<VacbedComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnAppearanceChange(EntityUid uid, VacbedComponent component, ref AppearanceChangeEvent args)
    {
        //todo figure out appearances and finish this

        if (args.Sprite == null) { return; }

        if (!_appearance.TryGetData<bool>(uid, VacbedComponent.VacbedVisuals.ContainsEntity, out var isEmpty, args.Component))
        {
            return;
        }

        if (isEmpty)
        {
            args.Sprite.LayerSetState(VacbedVisualLayers.Base, "vacbed");
        }
        else
        {
            args.Sprite.LayerSetState(VacbedVisualLayers.Base, "vacbed_filled");
        }
    }
}

//i'm genuinely too lazy to check if there's an easier way to handle visuals than this
//it shouldn't need a layer to work but SpriteComponent seems to assume it exists so here we are
public enum VacbedVisualLayers : byte
{
    Base,
}
