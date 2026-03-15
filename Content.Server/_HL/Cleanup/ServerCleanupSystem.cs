using System.Linq;
using System.Numerics;
using Content.Server.GameTicking;
using Content.Server.Ghost.Components;
using Content.Server.Mind;
using Content.Shared.Ghost;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._HL.Cleanup;

/// <summary>
/// Cleanup script that deletes all GRIDLESS entities if they aren't within 120m of a player. 
/// Also cleans up ghosts with no player attatched, and prevents orphan grids from being deleted if a player is on it
/// </summary>
public sealed class ServerCleanupSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;

    private ISawmill _sawmill = default!;
    private TimeSpan _nextGhostCleanup = TimeSpan.Zero;
    private TimeSpan _nextFloatingEntityCleanup = TimeSpan.Zero;

    /// <summary>
    /// How often to check for disconnected ghost players (default: 5 minutes).
    /// </summary>
    private static readonly TimeSpan GhostCleanupInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// How often to check for floating entities not on grids (default: 60 seconds).
    /// </summary>
    private static readonly TimeSpan FloatingEntityCleanupInterval = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Minimum distance (in tiles) a player must be from a floating entity
    /// </summary>
    private const float FloatingEntitySafeRadius = 120f;

    /// <summary>
    /// How long a player must be disconnected before ghost cleanup
    /// </summary>
    private static readonly TimeSpan DisconnectGracePeriod = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Maps player UserId to the time they were first detected as disconnected.
    /// Used to enforce the grace period before cleanup.
    /// </summary>
    private readonly Dictionary<Guid, TimeSpan> _disconnectedPlayers = new();

    public override void Initialize()
    {
        base.Initialize();
        _sawmill = Logger.GetSawmill("hl.cleanup");
        SubscribeLocalEvent<MapGridComponent, EntityTerminatingEvent>(OnGridTerminating);

        _sawmill.Info("HardLight Server Cleanup System initialized.");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _gameTiming.CurTime;

        if (curTime >= _nextGhostCleanup)
        {
            _nextGhostCleanup = curTime + GhostCleanupInterval;
            CleanupGhostPlayers();
        }

        if (curTime >= _nextFloatingEntityCleanup)
        {
            _nextFloatingEntityCleanup = curTime + FloatingEntityCleanupInterval;
            CleanupFloatingEntities();
        }
    }

    /// <summary>
    /// Scans for player ghosts whose sessions have disconnected and sends them back to the lobby after a grace period.
    /// </summary>
    private void CleanupGhostPlayers()
    {
        var curTime = _gameTiming.CurTime;
        var cleanedUp = 0;

        var connectedUsers = new HashSet<Guid>();
        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status == SessionStatus.InGame || session.Status == SessionStatus.Connected)
            {
                connectedUsers.Add(session.UserId);
            }
        }
        var query = EntityQueryEnumerator<MindContainerComponent, TransformComponent>();
        var entitiesToClean = new List<(EntityUid Uid, MindContainerComponent Mind)>();

        while (query.MoveNext(out var uid, out var mindContainer, out var xform))
        {
            if (EntityManager.IsQueuedForDeletion(uid) || !EntityManager.EntityExists(uid))
                continue;

            if (HasComp<GhostComponent>(uid))
                continue;

            if (!_mindSystem.TryGetMind(uid, out var mindId, out var mind))
                continue;

            if (mind.UserId == null)
                continue;

            var userId = mind.UserId.Value;

            if (connectedUsers.Contains(userId))
            {
                _disconnectedPlayers.Remove(userId);
                continue;
            }

            if (!_disconnectedPlayers.TryGetValue(userId, out var disconnectedSince))
            {
                _disconnectedPlayers[userId] = curTime;
                continue;
            }

            if (curTime - disconnectedSince < DisconnectGracePeriod)
                continue;

            entitiesToClean.Add((uid, mindContainer));
        }

        foreach (var (uid, _) in entitiesToClean)
        {
            if (!EntityManager.EntityExists(uid))
                continue;

            if (_mindSystem.TryGetMind(uid, out var mindId, out var mind) && mind.UserId != null)
            {
                _disconnectedPlayers.Remove(mind.UserId.Value);

                _sawmill.Info($"Cleaning up disconnected player entity {ToPrettyString(uid)} " +
                              $"(user: {mind.UserId}, disconnected for >{DisconnectGracePeriod.TotalMinutes:F0}m)");
            }
			
            QueueDel(uid);
            cleanedUp++;
        }

        var staleEntries = _disconnectedPlayers.Keys
            .Where(userId => !connectedUsers.Contains(userId))
            .ToList();

        if (cleanedUp > 0)
        {
            _sawmill.Info($"Ghost player cleanup: sent {cleanedUp} disconnected player(s) back to lobby.");
        }
    }

    /// <summary>
    /// Scans for physical entities that are not parented to any grid and deletes them if no player is within the safe radius.
    /// This cleans up stray mobs, items, unanchored objects, and any other debris that accumulate and waste entity calculations over time.
    ///
    /// Only targets entities with a PhysicsComponent
    /// This avoids touching engine internals, mind entities, action entities, UI entities, map entities, and other non-physical entities that live in nullspace or are parentless by design.
    /// </summary>
    private void CleanupFloatingEntities()
    {
        var deleted = 0;
        var playerPositions = new List<(MapId Map, Vector2 Pos)>();
        var playerQuery = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (playerQuery.MoveNext(out _, out _, out var playerXform))
        {
            // Skip players in nullspace (ghosts) — they aren't a reference point
            if (playerXform.MapID == MapId.Nullspace)
                continue;

            playerPositions.Add((playerXform.MapID, _transformSystem.GetWorldPosition(playerXform)));
        }
        var query = EntityQueryEnumerator<PhysicsComponent, TransformComponent>();
        var entitiesToDelete = new List<EntityUid>();

        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (!EntityManager.EntityExists(uid) || EntityManager.IsQueuedForDeletion(uid))
                continue;
			
            if (xform.MapID == MapId.Nullspace)
                continue;

            if (HasComp<ActorComponent>(uid))
                continue;

            if (HasComp<GhostComponent>(uid))
                continue;

            if (HasActivePlayerMind(uid))
                continue;

            if (HasComp<MapComponent>(uid) || HasComp<MapGridComponent>(uid))
                continue;

            if (xform.GridUid != null && EntityManager.EntityExists(xform.GridUid.Value))
                continue;

            if (IsAncestorOnGrid(xform))
                continue;
			
            var entityPos = _transformSystem.GetWorldPosition(xform);
            var entityMap = xform.MapID;
            var nearPlayer = false;

            foreach (var (playerMap, playerPos) in playerPositions)
            {
                if (playerMap != entityMap)
                    continue;

                var distance = Vector2.Distance(entityPos, playerPos);
                if (distance <= FloatingEntitySafeRadius)
                {
                    nearPlayer = true;
                    break;
                }
            }

            if (nearPlayer)
                continue;

            entitiesToDelete.Add(uid);
        }

        foreach (var uid in entitiesToDelete)
        {
            if (!EntityManager.EntityExists(uid))
                continue;

            _sawmill.Debug($"Deleting floating entity {ToPrettyString(uid)} (not on grid, no players within {FloatingEntitySafeRadius} tiles)");
            QueueDel(uid);
            deleted++;
        }

        if (deleted > 0)
        {
            _sawmill.Info($"Floating entity cleanup: deleted {deleted} stray entities.");
        }
    }

    /// <summary>
    /// Walks up the parent chain of an entity's transform to check whether any ancestor is parented to a grid.
	/// This catches entities inside containers or held by other entities that are themselves on a grid.
    /// </summary>
    private bool IsAncestorOnGrid(TransformComponent xform)
    {
        var current = xform;
        var depth = 0;

        while (current.ParentUid.IsValid() && depth < 20) // safety cap
        {
            if (current.GridUid != null && EntityManager.EntityExists(current.GridUid.Value))
                return true;

            if (!TryComp<TransformComponent>(current.ParentUid, out var parentXform))
                break;

            current = parentXform;
            depth++;
        }

        return false;
    }

    /// <summary>
    /// Checks whether an entity has a mind with an actively-connected player session.
    /// Returns false if the entity has no mind, the mind has no UserId, or the user is disconnected.
    /// </summary>
    private bool HasActivePlayerMind(EntityUid uid)
    {
        if (!_mindSystem.TryGetMind(uid, out _, out var mind))
            return false;

        if (mind.UserId == null)
            return false;

        foreach (var session in _playerManager.Sessions)
        {
            if (session.UserId == mind.UserId.Value
                && (session.Status == SessionStatus.InGame || session.Status == SessionStatus.Connected))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// When a grid is about to be deleted (from orphaned grid cleanup, shipyard save/delete, or any other source), this handler checks for players on/inside the grid
	
    private void OnGridTerminating(EntityUid gridUid, MapGridComponent grid, ref EntityTerminatingEvent args)
    {
        var rescued = 0;
        var xformQuery = GetEntityQuery<TransformComponent>();
        var actorQuery = GetEntityQuery<ActorComponent>();
        var mindQuery = GetEntityQuery<MindContainerComponent>();

        var playersToRescue = new List<EntityUid>();

        var childEnumerator = xformQuery.GetComponent(gridUid).ChildEnumerator;
        var visited = new HashSet<EntityUid>();
        var queue = new Queue<EntityUid>();

        while (childEnumerator.MoveNext(out var child))
        {
            queue.Enqueue(child);
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current))
                continue;

            if (!EntityManager.EntityExists(current))
                continue;

            if (actorQuery.HasComponent(current) || mindQuery.HasComponent(current))
            {
                if (actorQuery.HasComponent(current))
                {
                    playersToRescue.Add(current);
                }
            }

            if (xformQuery.TryGetComponent(current, out var childXform))
            {
                var subChildEnumerator = childXform.ChildEnumerator;
                while (subChildEnumerator.MoveNext(out var subChild))
                {
                    queue.Enqueue(subChild);
                }
            }
        }
		
        foreach (var playerUid in playersToRescue)
        {
            if (!EntityManager.EntityExists(playerUid))
                continue;

            if (RescuePlayerFromDeletingGrid(playerUid, gridUid))
                rescued++;
        }

        if (rescued > 0)
        {
            _sawmill.Warning($"Player protection: rescued {rescued} player(s) from grid {ToPrettyString(gridUid)} before deletion.");
        }
    }

    /// <summary>
    /// Relocates a player entity to a safe location when their current grid is being deleted.
	/// Tries to find a nearby grid to place them on, or spawns them as a ghost at the default map if no safe grid is found.
    /// </summary>
    private bool RescuePlayerFromDeletingGrid(EntityUid playerUid, EntityUid deletingGridUid)
    {
        if (!EntityManager.EntityExists(playerUid)
            || EntityManager.IsQueuedForDeletion(playerUid)
            || TerminatingOrDeleted(playerUid))
            return false;

        if (!TryComp<TransformComponent>(playerUid, out var playerXform))
            return false;

        var playerWorldPos = _transformSystem.GetWorldPosition(playerXform);
        var mapId = playerXform.MapID;

        _sawmill.Info($"Rescuing player {ToPrettyString(playerUid)} from deleting grid {ToPrettyString(deletingGridUid)}");

        EntityUid? bestGrid = null;
        var bestDistance = float.MaxValue;

        var gridQuery = EntityQueryEnumerator<MapGridComponent, TransformComponent>();
        while (gridQuery.MoveNext(out var gridUid, out _, out var gridXform))
        {
            if (gridUid == deletingGridUid)
                continue;

            if (gridXform.MapID != mapId)
                continue;

            if (!IsSafeRelocationTarget(gridUid))
                continue;

            var gridPos = _transformSystem.GetWorldPosition(gridXform);
            var distance = Vector2.Distance(playerWorldPos, gridPos);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestGrid = gridUid;
            }
        }

        if (bestGrid != null && bestDistance < 500f && IsSafeRelocationTarget(bestGrid.Value))
        {
            var targetCoords = new EntityCoordinates(bestGrid.Value, Vector2.Zero);
            if (TrySetCoordinatesSafely(playerUid, targetCoords))
            {
                _sawmill.Info($"Relocated player {ToPrettyString(playerUid)} to nearby grid {ToPrettyString(bestGrid.Value)}");
                return true;
            }
        }

        var mapUid = _mapManager.GetMapEntityId(mapId);
        if (IsSafeRelocationTarget(mapUid))
        {
            if (TrySetCoordinatesSafely(playerUid, new EntityCoordinates(mapUid, playerWorldPos)))
            {
                _sawmill.Info($"Detached player {ToPrettyString(playerUid)} from grid to map space.");
                return true;
            }
        }

        var defaultMapUid = _mapManager.GetMapEntityId(_gameTicker.DefaultMap);
        if (IsSafeRelocationTarget(defaultMapUid))
        {
            if (TrySetCoordinatesSafely(playerUid, new EntityCoordinates(defaultMapUid, Vector2.Zero)))
            {
                _sawmill.Warning($"Emergency relocation: moved player {ToPrettyString(playerUid)} to default map origin.");
                return true;
            }
        }

        _sawmill.Warning($"Could not find safe relocation target for player {ToPrettyString(playerUid)} while grid {ToPrettyString(deletingGridUid)} was terminating.");
        return false;
    }

    private bool IsSafeRelocationTarget(EntityUid uid)
    {
        return uid.IsValid()
               && EntityManager.EntityExists(uid)
               && !EntityManager.IsQueuedForDeletion(uid)
               && !TerminatingOrDeleted(uid);
    }

    private bool TrySetCoordinatesSafely(EntityUid entityUid, EntityCoordinates coordinates)
    {
        if (!EntityManager.EntityExists(entityUid)
            || EntityManager.IsQueuedForDeletion(entityUid)
            || TerminatingOrDeleted(entityUid)
            || !IsSafeRelocationTarget(coordinates.EntityId))
            return false;

        try
        {
            _transformSystem.SetCoordinates(entityUid, coordinates);
            return true;
        }
        catch (Exception ex)
        {
            _sawmill.Warning($"Failed to relocate {ToPrettyString(entityUid)} to {ToPrettyString(coordinates.EntityId)} during grid termination: {ex.Message}");
            return false;
        }
    }
}