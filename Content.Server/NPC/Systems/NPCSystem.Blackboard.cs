using System.Diagnostics.CodeAnalysis;
using Content.Server.NPC.HTN;

namespace Content.Server.NPC.Systems;

public sealed partial class NPCSystem
{
    public void SetBlackboard(EntityUid uid, string key, object value, HTNComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
        {
            return;
        }

        var blackboard = component.Blackboard;
        blackboard.SetValue(key, value);
    }

    public bool TryGetBlackboardValue<T>(EntityUid uid, string key, [NotNullWhen(true)] out T? value, HTNComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
        {
            value = default;
            return false;
        }

        return component.Blackboard.TryGetValue(key, out value, EntityManager);
    }
}
