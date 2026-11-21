using Content.Shared._KS14.PredictedSpawning;
using Content.Shared.Projectiles;
using Content.Shared.Whitelist;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Battlefield14.Projectiles;

/// <summary>
///     Splits <see cref="KsSubmunitionProjectileComponent"/> projectiles: decides when, on the server's copy and on a
///         shooter's kept copy alike, and deletes them. The server spawns the submunitions (<see cref="SpawnSubmunitions"/>).
/// </summary>
public abstract partial class SharedKsSubmunitionProjectileSystem : EntitySystem
{
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private INetManager _netManager = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private KsSharedPredictedSpawnSystem _predictedSpawnSystem = default!;

    /// <summary>
    ///     The projectiles due to split this tick, gathered before any is split: splitting spawns more of them.
    /// </summary>
    private readonly List<Entity<KsSubmunitionProjectileComponent>> _due = new();

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<KsSubmunitionProjectileComponent> entity, ref MapInitEvent args)
    {
        // A projectile is spawned where it is fired from, the kept copy and the server's alike.
        entity.Comp.FiredAt = _gameTiming.CurTime;
        entity.Comp.OriginPosition = _transformSystem.GetWorldPosition(entity.Owner);
        Dirty(entity);
    }

    [SubscribeLocalEvent]
    private void OnProjectileHit(Entity<KsSubmunitionProjectileComponent> entity, ref ProjectileHitEvent args)
    {
        if (entity.Comp.Blocked || !_whitelistSystem.IsWhitelistPass(entity.Comp.Blacklist, args.Target))
            return;

        entity.Comp.Blocked = true;
        Dirty(entity);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<KsSubmunitionProjectileComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.Split || component.Blocked || !IsSimulated(uid) || !IsDue((uid, component)))
                continue;

            _due.Add((uid, component));
        }

        foreach (var entity in _due)
        {
            entity.Comp.Split = true;
            SpawnSubmunitions(entity);
            _predictedSpawnSystem.PredictedQueueDelDeferrable(entity.Owner);
        }

        _due.Clear();
    }

    /// <summary>
    ///     Whether this side decides when <paramref name="uid"/> splits: the server for its own, and a client only for
    ///         its kept copies. Every other copy on a client is the server's, and goes when the server deletes it.
    /// </summary>
    private bool IsSimulated(EntityUid uid)
    {
        return _netManager.IsServer || _predictedSpawnSystem.IsKeptLocally(uid);
    }

    /// <summary>
    ///     Whether the projectile has flown far enough, or long enough, to split. Measured from where and when it was
    ///         fired rather than added up tick by tick, so a re-predicted tick can't count twice.
    /// </summary>
    private bool IsDue(Entity<KsSubmunitionProjectileComponent> entity)
    {
        if (entity.Comp.DistanceDelay > 0f)
        {
            var travelled = _transformSystem.GetWorldPosition(entity.Owner) - entity.Comp.OriginPosition;
            return travelled.LengthSquared() >= entity.Comp.DistanceDelay * entity.Comp.DistanceDelay;
        }

        return _gameTiming.CurTime >= entity.Comp.FiredAt + entity.Comp.SubmunitionDelay;
    }

    /// <summary>
    ///     Spawns what <paramref name="entity"/> splits into, just before it is deleted. Server only.
    /// </summary>
    protected virtual void SpawnSubmunitions(Entity<KsSubmunitionProjectileComponent> entity)
    {
    }
}
