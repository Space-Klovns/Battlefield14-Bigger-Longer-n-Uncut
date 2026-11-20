// Spawning taken from Battlefield14 (https://github.com/PunishedJoe/Battlefield14) at
//      d24a6e8c2f5d995e6bdd0a2571e406923e53f883, Content.Server/_Battlefield14/Projectiles/Systems/SubmunitionProjectileSystem.cs

using System.Numerics;
using Content.Server.Administration.Logs;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared._Battlefield14.Projectiles;
using Content.Shared.Database;
using Content.Shared.Projectiles;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;

namespace Content.Server._Battlefield14.Projectiles;

/// <inheritdoc/>
public sealed partial class KsSubmunitionProjectileSystem : SharedKsSubmunitionProjectileSystem
{
    [Dependency] private IAdminLogManager _adminLogManager = default!;
    [Dependency] private GunSystem _gunSystem = default!;
    [Dependency] private SharedPhysicsSystem _physicsSystem = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private EntityQuery<ProjectileComponent> _projectileQuery = default!;

    /// <summary>
    ///     How far ahead of the parent, along its own direction, each submunition starts.
    /// </summary>
    private const float SpawnOffset = 0.3f;

    /// <summary>
    ///     Below this speed squared the parent's velocity says nothing of its direction, and its rotation is used.
    /// </summary>
    private const float MinDirectionSpeedSquared = 0.01f;

    protected override void SpawnSubmunitions(Entity<KsSubmunitionProjectileComponent> entity)
    {
        var component = entity.Comp;
        _adminLogManager.Add(LogType.Action, LogImpact.Medium,
            $"Submunitions deployed from {ToPrettyString(entity.Owner):projectile} - spawned {component.SubmunitionCount} submunitions of type {component.SubmunitionPrototype}");

        var coordinates = _transformSystem.GetMapCoordinates(entity.Owner);
        var velocity = _physicsSystem.GetMapLinearVelocity(entity.Owner);
        var baseDirection = velocity.LengthSquared() > MinDirectionSpeedSquared
            ? velocity.Normalized()
            : _transformSystem.GetWorldRotation(entity.Owner).ToVec();
        var baseAngle = baseDirection.ToWorldAngle();
        var speed = velocity.Length() * component.VelocityMultiplier;

        var spreadPerSubmunition = component.SpreadAngle / Math.Max(1, component.SubmunitionCount - 1);
        var startAngle = baseAngle - Angle.FromDegrees(component.SpreadAngle / 2f);

        _projectileQuery.TryComp(entity.Owner, out var parentProjectile);

        for (var i = 0; i < component.SubmunitionCount; i++)
        {
            var direction = component.SubmunitionCount == 1
                ? baseDirection
                : (startAngle + Angle.FromDegrees(spreadPerSubmunition * i)).ToVec();

            var submunitionUid = Spawn(component.SubmunitionPrototype, new MapCoordinates(coordinates.Position + direction * SpawnOffset, coordinates.MapId));

            // The parent's shooter and weapon, as BF14 did, but outside any user's prediction scope: the shooter's
            //      client sees the server's submunitions like everyone else.
            _gunSystem.ShootProjectile(submunitionUid, direction, Vector2.Zero, parentProjectile?.Weapon, parentProjectile?.Shooter, speed);
        }
    }
}
