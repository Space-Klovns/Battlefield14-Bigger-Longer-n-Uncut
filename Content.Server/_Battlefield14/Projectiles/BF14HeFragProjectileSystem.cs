// Taken from Battlefield14 (https://github.com/PunishedJoe/Battlefield14) at d24a6e8c2f5d995e6bdd0a2571e406923e53f883,
//      Content.Server/Explosion/EntitySystems/ProjectileFragSystem.cs

using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Projectiles;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
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
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private TransformSystem _transformSystem = default!;
    [Dependency] private EntityQuery<ProjectileComponent> _projectileQuery = default!;

    /// <summary>
    ///     How far from the hit each fragment starts, along its own direction, so they don't all hit the same thing.
    /// </summary>
    private const float SpawnOffset = 0.5f;

    [SubscribeLocalEvent]
    private void OnInit(Entity<BF14HeFragProjectileComponent> entity, ref ComponentInit args)
    {
        entity.Comp.Container = _containerSystem.EnsureContainer<Container>(entity.Owner, "frag-payload");
    }

    [SubscribeLocalEvent]
    private void OnStartup(Entity<BF14HeFragProjectileComponent> entity, ref ComponentStartup args)
    {
        if (entity.Comp.FillPrototype == null)
            return;

        entity.Comp.UnspawnedCount = Math.Max(0, entity.Comp.Capacity - entity.Comp.Container.ContainedEntities.Count);
    }

    [SubscribeLocalEvent]
    private void OnProjectileHit(Entity<BF14HeFragProjectileComponent> entity, ref ProjectileHitEvent args)
    {
        FragmentIntoProjectiles(entity);
    }

    private void FragmentIntoProjectiles(Entity<BF14HeFragProjectileComponent> entity)
    {
        var totalCount = entity.Comp.Container.ContainedEntities.Count + entity.Comp.UnspawnedCount;
        if (totalCount <= 0)
            return;

        var hitCoordinates = _transformSystem.GetMapCoordinates(entity.Owner);
        var segmentAngle = 360 / totalCount;

        // BF14 made the round itself the fragments' weapon. It is deleted with this hit, and the weapon is networked, so
        //      it is the gun that fired the round instead: the fragments still hit the shooter, as BF14's did.
        var weaponUid = _projectileQuery.CompOrNull(entity.Owner)?.Weapon;
        var shootCount = 0;

        while (TrySpawnContents(hitCoordinates, entity.Comp, out var fragmentUid))
        {
            Angle angle;
            if (entity.Comp.RandomAngle)
                angle = _random.NextAngle();
            else
            {
                angle = Angle.FromDegrees(_random.Next(segmentAngle * shootCount, segmentAngle * (shootCount + 1)));
                shootCount++;
            }

            // The random velocity is BF14's: it makes the burst look uneven.
            var direction = angle.ToVec().Normalized();
            var velocity = _random.NextVector2(entity.Comp.MinVelocity, entity.Comp.MaxVelocity);

            _transformSystem.SetMapCoordinates(fragmentUid, new MapCoordinates(hitCoordinates.Position + direction * SpawnOffset, hitCoordinates.MapId));

            // No user: a fragment is nobody's prediction, so every client, the shooter's too, sees the server's.
            _gunSystem.ShootProjectile(fragmentUid, direction, velocity, weaponUid);
        }
    }

    /// <summary>
    ///     Takes the next fragment: a fresh one while any are left to spawn, then whatever the container holds.
    /// </summary>
    private bool TrySpawnContents(MapCoordinates coordinates, BF14HeFragProjectileComponent component, out EntityUid fragmentUid)
    {
        fragmentUid = default;

        if (component.UnspawnedCount > 0)
        {
            component.UnspawnedCount--;
            fragmentUid = Spawn(component.FillPrototype, coordinates);
            return true;
        }

        if (component.Container.ContainedEntities.Count == 0)
            return false;

        fragmentUid = component.Container.ContainedEntities[0];
        return _containerSystem.Remove(fragmentUid, component.Container);
    }
}
