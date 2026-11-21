// BF14-BLU: ported from Monolith via Battlefield14 (https://github.com/PunishedJoe/Battlefield14) at d24a6e8c2f5d995e6bdd0a2571e406923e53f883
using System.Numerics;
using Content.Shared.Physics;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._Mono.ArmorPiercing;

/// <summary>
/// Handles collision logic for projectiles with ArmorPiercingComponent.
/// </summary>
public sealed class ArmorPiercingSystem : EntitySystem
{
    // BF14-BLU start: what the round's path this tick crosses
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private EntityLookupSystem _lookupSystem = default!;
    [Dependency] private SharedPhysicsSystem _physicsSystem = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;

    /// <summary>
    ///     How far inside a wall's bounds the path has to go to cross it, so one only grazed along its edge is not.
    /// </summary>
    private const float CrossingMargin = 0.01f;
    // BF14-BLU end

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArmorPiercingComponent, PreventCollideEvent>(OnPreventCollide);
    }

    private void OnPreventCollide(Entity<ArmorPiercingComponent> ent, ref PreventCollideEvent args)
    {
        var comp = ent.Comp;

        var isWall = (args.OtherFixture.CollisionLayer & (int)CollisionGroup.Impassable) != 0;

        if (!isWall)
            return;

        if (!TryComp(args.OtherEntity, out ArmorThicknessComponent? armorThickness))
            return;

        if (!armorThickness.CanBePierced)
            return;

        var alreadyPierced = comp.PiercedEntities.Contains(args.OtherEntity);

        if (alreadyPierced)
        {
            args.Cancelled = true;
            return;
        }

        if (comp.PiercingThickness < armorThickness.Thickness)
            return;

        args.Cancelled = true;

        // BF14-BLU: only a wall the round really goes through this tick spends its thickness. KS14 asks this of every
        //      wall near its path (KsRayCollisionSystem's look ahead goes through the broadphase), and Mono's took each
        //      ask for a pierce: one beside the line of fire halved the round before it reached the wall in front of it
        if (!IsCrossing(ent.Owner, args.OtherEntity))
            return;

        comp.PiercedEntities.Add(args.OtherEntity);

        comp.PiercingThickness = Math.Max(1, comp.PiercingThickness / 2);

        Dirty(ent, comp);
    }

    // BF14-BLU start
    /// <summary>
    ///     Whether <paramref name="projectile"/>'s path this tick, from where it is to where its velocity takes it,
    ///         goes into <paramref name="wall"/>'s bounds.
    /// </summary>
    private bool IsCrossing(EntityUid projectile, EntityUid wall)
    {
        var start = _transformSystem.GetWorldPosition(projectile);
        var end = start + _physicsSystem.GetMapLinearVelocity(projectile) * (float)_gameTiming.TickPeriod.TotalSeconds;
        var bounds = _lookupSystem.GetWorldAABB(wall).Enlarged(-CrossingMargin);
        return SegmentIntersects(bounds, start, end);
    }

    /// <summary>
    ///     Whether the segment from <paramref name="start"/> to <paramref name="end"/> touches <paramref name="box"/>
    ///         (slab test).
    /// </summary>
    public static bool SegmentIntersects(Box2 box, Vector2 start, Vector2 end)
    {
        var delta = end - start;
        var enter = 0f;
        var exit = 1f;

        for (var axis = 0; axis < 2; axis++)
        {
            var origin = axis == 0 ? start.X : start.Y;
            var direction = axis == 0 ? delta.X : delta.Y;
            var min = axis == 0 ? box.Left : box.Bottom;
            var max = axis == 0 ? box.Right : box.Top;

            if (MathF.Abs(direction) < float.Epsilon)
            {
                if (origin < min || origin > max)
                    return false;

                continue;
            }

            var t1 = (min - origin) / direction;
            var t2 = (max - origin) / direction;
            enter = MathF.Max(enter, MathF.Min(t1, t2));
            exit = MathF.Min(exit, MathF.Max(t1, t2));
            if (enter > exit)
                return false;
        }

        return true;
    }
    // BF14-BLU end
}
