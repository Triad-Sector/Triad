using Robust.Shared.GameObjects;

namespace Content.Shared.CM14.Xenos.Evolution;

public sealed class XenoEvolvedEvent : EntityEventArgs
{
    public EntityUid Old { get; }
    public EntityUid New { get; }

    public XenoEvolvedEvent(EntityUid oldEntity, EntityUid newEntity)
    {
        Old = oldEntity;
        New = newEntity;
    }
}
