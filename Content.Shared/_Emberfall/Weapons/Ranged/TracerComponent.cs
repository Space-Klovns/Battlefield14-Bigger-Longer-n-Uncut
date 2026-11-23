// Taken from Emberfall via Battlefield14 (https://github.com/PunishedJoe/Battlefield14) at
//      d24a6e8c2f5d995e6bdd0a2571e406923e53f883, Content.Shared/_Emberfall/Weapons/Ranged/TracerComponent.cs

using System.Numerics;
using Robust.Shared.Map;

namespace Content.Shared._Emberfall.Weapons.Ranged;

/// <summary>
///     Draws a line along the path a projectile has just flown.
/// </summary>
/// <remarks>
///     BF14-BLU: client only, and not networked: each client draws the tracer of the copy of a shot it shows, the
///         shooter's own kept copy or everyone else's server copy (<c>KsPredictedSpawnSystem.IsShownHere</c>), so it
///         appears the moment the shot is fired and never twice. The path is recorded once a rendered frame rather
///         than once a tick, as a client runs a tick again and again while re-predicting.
/// </remarks>
[RegisterComponent]
public sealed partial class TracerComponent : Component
{
    /// <summary>
    ///     How long, in seconds, the tracer is drawn for after the projectile appears.
    /// </summary>
    [DataField]
    public float Lifetime = 10f;

    /// <summary>
    ///     The longest the line gets, in tiles.
    /// </summary>
    [DataField]
    public float Length = 2f;

    /// <summary>
    ///     The line's colour.
    /// </summary>
    [DataField]
    public Color Color = Color.Red;

    /// <summary>
    ///     When it stops being drawn. Client only.
    /// </summary>
    [ViewVariables]
    public TimeSpan EndTime;

    /// <summary>
    ///     The map <see cref="PositionHistory"/> is on; the history starts again when the projectile leaves it.
    /// </summary>
    [ViewVariables]
    public MapId Map = MapId.Nullspace;

    /// <summary>
    ///     Where the projectile has been, oldest first, at most <see cref="Length"/> long. Client only.
    /// </summary>
    [ViewVariables]
    public List<Vector2> PositionHistory = new();
}
