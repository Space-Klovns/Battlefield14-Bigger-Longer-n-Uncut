using System.Numerics;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._Battlefield14.Projectiles;

/// <summary>
///     A projectile that turns into others in flight, after <see cref="SubmunitionDelay"/> or, when it is set, after
///         flying <see cref="DistanceDelay"/>: submunitions, a sabot's penetrator, a rocket's arming distance.
///         Battlefield14's <c>SubmunitionProjectile</c>, split so that a shooter's kept copy goes at the same moment.
/// </summary>
/// <remarks>
///     When to split is worked out in shared code, on the server's copy and on the shooter's kept copy alike
///         (<c>KsSharedPredictedSpawnSystem</c>), so the kept copy is gone the tick the server splits, not a round trip
///         later. Only the server spawns the submunitions, with no user, and everyone sees those.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(SharedKsSubmunitionProjectileSystem))]
public sealed partial class KsSubmunitionProjectileComponent : Component
{
    /// <summary>
    ///     What the projectile turns into.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public EntProtoId SubmunitionPrototype;

    /// <summary>
    ///     How long after it is fired it splits, unless <see cref="DistanceDelay"/> is set. Zero or less splits at once.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan SubmunitionDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     How far it flies before it splits. Used instead of <see cref="SubmunitionDelay"/> when greater than zero.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float DistanceDelay;

    /// <summary>
    ///     How many submunitions it splits into.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int SubmunitionCount = 1;

    /// <summary>
    ///     The arc, in degrees, the submunitions are spread evenly across; 0 sends them all straight on.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SpreadAngle;

    /// <summary>
    ///     A submunition's speed, as a multiple of the projectile's when it split.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float VelocityMultiplier = 1f;

    /// <summary>
    ///     Hitting something on this list stops the projectile from ever splitting: a rocket that hits a person before its
    ///         arming distance stays a dud.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityWhitelist? Blacklist;

    /// <summary>
    ///     When it was fired; <see cref="SubmunitionDelay"/> counts from here.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan FiredAt;

    /// <summary>
    ///     Where it was fired from; <see cref="DistanceDelay"/> is measured from here.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Vector2 OriginPosition;

    /// <summary>
    ///     Whether it hit something on <see cref="Blacklist"/>, so it will never split.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public bool Blocked;

    /// <summary>
    ///     Whether it has split, and is only waiting to be deleted.
    /// </summary>
    [ViewVariables]
    public bool Split;
}
