using Content.Server.NPC;
using Content.Server.NPC.Systems;
using Content.Shared.CM14.Xenos.Evolution;
using Robust.Shared.Map;

namespace Content.Server.CM14.Xenos.Evolution;

public sealed class XenoEvolutionFollowSystem : EntitySystem
{
    [Dependency] private readonly NPCSystem _npc = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XenoEvolvedEvent>(OnXenoEvolved);
    }

    private void OnXenoEvolved(XenoEvolvedEvent ev)
    {
        if (_npc.TryGetBlackboardValue(ev.Old, NPCBlackboard.FollowTarget, out EntityCoordinates followTarget))
        {
            _npc.SetBlackboard(ev.New, NPCBlackboard.FollowTarget, followTarget);
        }
    }
}
