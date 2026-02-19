using Content.Shared.Actions;

namespace Content.Shared._HL.Brainwashing;

public class SharedBrainwashedSystem : EntitySystem
{
    public bool SetCompulsions(EntityUid uid, List<string> compulsions)
    {
        var hasComponent = TryComp<BrainwashedComponent>(uid, out var brainwashedComponent);
        if (!hasComponent || brainwashedComponent == null)
            return false;
        return SetCompulsions(uid, brainwashedComponent, compulsions);
    }

    public bool SetCompulsions(EntityUid uid, BrainwashedComponent brainwashedComponent, List<string> compulsions)
    {
        brainwashedComponent.Compulsions = compulsions;
        DirtyField(uid, brainwashedComponent, nameof(brainwashedComponent.Compulsions));
        return true;
    }
}

public sealed partial class OpenCompulsionsMenuAction : InstantActionEvent;
