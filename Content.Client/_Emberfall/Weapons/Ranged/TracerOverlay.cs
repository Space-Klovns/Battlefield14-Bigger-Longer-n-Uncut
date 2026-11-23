// Taken from Emberfall via Battlefield14 (https://github.com/PunishedJoe/Battlefield14) at
//      d24a6e8c2f5d995e6bdd0a2571e406923e53f883, Content.Client/_Emberfall/Weapons/Ranged/Overlays/Tracer/TracerOverlay.cs

using System.Numerics;
using Content.Shared._Emberfall.Weapons.Ranged;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;

namespace Content.Client._Emberfall.Weapons.Ranged;

/// <summary>
///     Draws each <see cref="TracerComponent"/>'s recorded path as a line.
/// </summary>
public sealed partial class TracerOverlay : Overlay
{
    [Dependency] private IEntityManager _entityManager = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpaceEntities;

    /// <summary>
    ///     The paths to draw in this pass: gathered in <see cref="BeforeDraw"/>, drawn by the <see cref="Draw"/> after it.
    /// </summary>
    private readonly List<TracerComponent> _visible = new();

    public TracerOverlay()
    {
        // BF14-BLU: among the effects, as KsTrail is; with no ZIndex it drew under every sprite
        ZIndex = (int)Shared.DrawDepth.DrawDepth.Effects;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        _visible.Clear();

        if (args.MapId == MapId.Nullspace)
            return false;

        // BF14-BLU: by map, as each z-level is drawn in its own pass
        var enumerator = _entityManager.EntityQueryEnumerator<TracerComponent>();
        while (enumerator.MoveNext(out var tracerComponent))
        {
            if (tracerComponent.Map == args.MapId && tracerComponent.PositionHistory.Count >= 2)
                _visible.Add(tracerComponent);
        }

        return _visible.Count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var worldHandle = args.WorldHandle;
        worldHandle.SetTransform(Matrix3x2.Identity);

        foreach (var tracerComponent in _visible)
        {
            var positions = tracerComponent.PositionHistory;
            for (var i = 1; i < positions.Count; i++)
                worldHandle.DrawLine(positions[i - 1], positions[i], tracerComponent.Color);
        }
    }
}
