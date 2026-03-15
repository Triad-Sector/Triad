using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;
using TimedDespawnComponent = Robust.Shared.Spawners.TimedDespawnComponent;

namespace Content.Server.Administration.Systems;

/// <summary>
/// This handles the administrative test arena maps, and loading them.
/// </summary>
public sealed class AdminTestArenaSystem : EntitySystem
{
    [Dependency] private readonly MapLoaderSystem _loader = default!;
    [Dependency] private readonly MetaDataSystem _metaDataSystem = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;

    private static readonly TimeSpan ArenaDespawnDelay = TimeSpan.FromMinutes(30);

    public const string ArenaMapPath = "/Maps/_NF/Test/admin_test_zone.yml"; // Frontier: Map edit, swap /Maps/Test/admin_test_arena.yml

    public Dictionary<NetUserId, EntityUid> ArenaMap { get; private set; } = new();
    public Dictionary<NetUserId, EntityUid?> ArenaGrid { get; private set; } = new();

    public (EntityUid Map, EntityUid? Grid) AssertArenaLoaded(ICommonSession admin)
    {
        if (ArenaMap.TryGetValue(admin.UserId, out var arenaMap) && !Deleted(arenaMap) && !Terminating(arenaMap))
        {
            if (ArenaGrid.TryGetValue(admin.UserId, out var arenaGrid) && !Deleted(arenaGrid) && !Terminating(arenaGrid.Value))
            {
                return (arenaMap, arenaGrid);
            }


            ArenaGrid[admin.UserId] = null;
            return (arenaMap, null);
        }

        var path = new ResPath(ArenaMapPath);
        var mapUid = _maps.CreateMap(out var mapId);

        if (!_loader.TryLoadGrid(mapId, path, out var grid))
        {
            QueueDel(mapUid);
            throw new Exception($"Failed to load admin arena");
        }

        ArenaMap[admin.UserId] = mapUid;
        _metaDataSystem.SetEntityName(mapUid, $"ATAM-{admin.Name}");

        var mapDespawn = EnsureComp<TimedDespawnComponent>(mapUid);
        mapDespawn.Lifetime = (float) ArenaDespawnDelay.TotalSeconds;

        ArenaGrid[admin.UserId] = grid.Value.Owner;
        _metaDataSystem.SetEntityName(grid.Value.Owner, $"ATAG-{admin.Name}");

        var gridDespawn = EnsureComp<TimedDespawnComponent>(grid.Value.Owner);
        gridDespawn.Lifetime = (float) ArenaDespawnDelay.TotalSeconds;

        return (mapUid, grid.Value.Owner);
    }
}
