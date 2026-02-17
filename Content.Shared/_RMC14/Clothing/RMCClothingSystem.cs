using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Item;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Clothing;

public sealed class RMCClothingSystem : EntitySystem
{
    [Dependency] private readonly ClothingSystem _clothing = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RMCClothingFoldableComponent, GetVerbsEvent<AlternativeVerb>>(AddFoldVerb);
    }


    private void AddFoldVerb(Entity<RMCClothingFoldableComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        foreach (var type in ent.Comp.Types)
        {
            AlternativeVerb verb = new()
            {
                Act = () => TryToggleFold(ent, type, user),
                Text = Loc.GetString(type.Name),
                Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/fold.svg.192dpi.png")),
                Priority = type.Priority,
            };

            args.Verbs.Add(verb);
        }
    }

    public void TryToggleFold(Entity<RMCClothingFoldableComponent> ent, FoldableType type, EntityUid? user)
    {
        if (type.Prefix == ent.Comp.ActivatedPrefix) // already activated
        {
            SetPrefix(ent, null, false);
        }
        else
        {
            if (type.BlacklistedPrefix == ent.Comp.ActivatedPrefix && ent.Comp.ActivatedPrefix != null)
            {
                if (type.BlacklistPopup != null && user != null)
                {
                    var msg = Loc.GetString(type.BlacklistPopup);
                    _popup.PopupClient(msg, user.Value, user.Value, PopupType.SmallCaution);
                }

                return;
            }

            SetPrefix(ent, type.Prefix, type.HideAccessories);
        }
    }

    public void SetPrefix(Entity<RMCClothingFoldableComponent> ent, string? prefix, bool hideAccessories)
    {
        ent.Comp.ActivatedPrefix = prefix;
        Dirty(ent);

        _clothing.SetEquippedPrefix(ent.Owner, ent.Comp.ActivatedPrefix);
    }
}
