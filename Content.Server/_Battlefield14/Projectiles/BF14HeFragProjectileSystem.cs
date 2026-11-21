// Taken from Battlefield14 (https://github.com/PunishedJoe/Battlefield14) at d24a6e8c2f5d995e6bdd0a2571e406923e53f883,
//      Content.Server/Explosion/EntitySystems/ProjectileFragSystem.cs

using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Projectiles;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server._Battlefield14.Projectiles;

/// <summary>
///     Bursts a <see cref="BF14HeFragProjectileComponent"/> into its fragments where it hits something.
/// </summary>
public sealed partial class BF14HeFragProjectileSystem : EntitySystem
{
    [Dependency] private GunSystem _gunSystem = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private TransformSystem _transformSystem = default!;
    [Dependency] private EntityQuery<ProjectileComponent> _projectileQuery = default!;

    /// <summary>
    ///     How far from the hit each fragment starts, along its own direction, so they don't all hit the same thing.
    /// </summary>
    private const float SpawnOffset = 0.5f;

    [SubscribeLocalEvent]
    private void OnProjectileHit(Entity<BF14HeFragProjectileComponent> entity, ref ProjectileHitEvent args)
    {
        // Once: a round that goes through what it hits bursts at the first thing only, as BF14's emptied its payload.
        if (entity.Comp.Burst || entity.Comp.FillPrototype is not { } fillPrototype || entity.Comp.Capacity <= 0)
            return;

        entity.Comp.Burst = true;

        var hitCoordinates = _transformSystem.GetMapCoordinates(entity.Owner);
        var segmentAngle = 360 / entity.Comp.Capacity;

        // BF14 made the round itself the fragments' weapon. It is deleted with this hit, and the weapon is networked, so
        //      it is the gun that fired the round instead: the fragments still hit the shooter, as BF14's did.
        var weaponUid = _projectileQuery.CompOrNull(entity.Owner)?.Weapon;

        for (var i = 0; i < entity.Comp.Capacity; i++)
        {
            var angle = entity.Comp.RandomAngle
                ? _random.NextAngle()
                : Angle.FromDegrees(_random.Next(segmentAngle * i, segmentAngle * (i + 1)));

            // The random velocity is BF14's: it makes the burst look uneven.
            var direction = angle.ToVec().Normalized();
            var velocity = _random.NextVector2(entity.Comp.MinVelocity, entity.Comp.MaxVelocity);

            var fragmentUid = Spawn(fillPrototype, new MapCoordinates(hitCoordinates.Position + direction * SpawnOffset, hitCoordinates.MapId));

            // No user: a fragment is nobody's prediction, so every client, the shooter's too, sees the server's.
            _gunSystem.ShootProjectile(fragmentUid, direction, velocity, weaponUid);
        }
    }
}
