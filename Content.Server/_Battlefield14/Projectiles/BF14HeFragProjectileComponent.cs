// Taken from Battlefield14 (https://github.com/PunishedJoe/Battlefield14) at d24a6e8c2f5d995e6bdd0a2571e406923e53f883,
//      Content.Server/Explosion/Components/HeFragProjectileComponent.cs

using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._Battlefield14.Projectiles;

/// <summary>
///     A projectile that bursts into fragments where it hits something.
/// </summary>
/// <remarks>
///     Server only. A shooter's client keeps its own copy of the round (<c>KsSharedPredictedSpawnSystem</c>), which just
///         hits and deletes itself; the fragments are the server's, spawned with no user, so everyone sees the same set.
/// </remarks>
[RegisterComponent, Access(typeof(BF14HeFragProjectileSystem))]
public sealed partial class BF14HeFragProjectileComponent : Component
{
    public Container Container = default!;

    /// <summary>
    ///     The fragment the projectile is filled with.
    /// </summary>
    [DataField]
    public EntProtoId? FillPrototype;

    /// <summary>
    ///     How many of <see cref="FillPrototype"/> are still to be spawned, on top of what <see cref="Container"/> holds.
    /// </summary>
    [ViewVariables]
    public int UnspawnedCount;

    /// <summary>
    ///     The total number of fragments.
    /// </summary>
    [DataField]
    public int Capacity = 3;

    /// <summary>
    ///     Whether each fragment flies off at a random angle, rather than one in each of <see cref="Capacity"/> even arcs.
    /// </summary>
    [DataField]
    public bool RandomAngle;

    /// <summary>
    ///     The least random velocity a fragment gets, on top of its projectile speed.
    /// </summary>
    [DataField]
    public float MinVelocity = 2f;

    /// <summary>
    ///     The most random velocity a fragment gets, on top of its projectile speed.
    /// </summary>
    [DataField]
    public float MaxVelocity = 6f;
}
