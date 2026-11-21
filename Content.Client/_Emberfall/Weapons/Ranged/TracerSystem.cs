// Taken from Emberfall via Battlefield14 (https://github.com/PunishedJoe/Battlefield14) at
//      d24a6e8c2f5d995e6bdd0a2571e406923e53f883, Content.Client/_Emberfall/Weapons/Ranged/Overlays/Systems/TracerSystem.cs

using System.Numerics;
using Content.Client._KS14.PredictedSpawning;
using Content.Shared._Emberfall.Weapons.Ranged;
using Content.Shared._KS14.IoC;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client._Emberfall.Weapons.Ranged;

/// <summary>
///     Records the path of every <see cref="TracerComponent"/> projectile this client shows, for
///         <see cref="TracerOverlay"/> to draw.
/// </summary>
public sealed partial class TracerSystem : EntitySystem
{
    [Dependency] private IGameTiming _gameTiming = default!;
    [Dependency] private IOverlayManager _overlayManager = default!;
    [Dependency] private SystemCollectionHookManager _hookManager = default!;
    [Dependency] private SharedTransformSystem _transformSystem = default!;
    [Dependency] private KsPredictedSpawnSystem _predictedSpawnSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        // BF14-BLU: after the transforms are interpolated for the frame, so the path is the one drawn
        UpdatesAfter.Add(typeof(TransformSystem));

        _hookManager.HookAction(OnDependenciesReady);
    }

    private void OnDependenciesReady(IDependencyCollection dependencyCollection)
    {
        var overlay = new TracerOverlay();
        dependencyCollection.InjectDependencies(overlay, oneOff: true);
        _overlayManager.AddOverlay(overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlayManager.RemoveOverlay<TracerOverlay>();
    }

    [SubscribeLocalEvent]
    private void OnStartup(Entity<TracerComponent> entity, ref ComponentStartup args)
    {
        entity.Comp.EndTime = _gameTiming.CurTime + TimeSpan.FromSeconds(entity.Comp.Lifetime);
    }

    /// <summary>
    ///     Whether <paramref name="uid"/>'s tracer is drawn here: only for the copy of a shot this client shows, so the
    ///         shooter sees its own kept copy's and never a second from the hidden server copy or a re-predicted
    ///         throwaway.
    /// </summary>
    public bool IsDrawn(EntityUid uid)
    {
        return _predictedSpawnSystem.IsShownHere(uid);
    }

    // BF14-BLU: once a frame, not in Update: a client runs a tick again for each re-prediction, and the history would
    //      go back and forth along the path each time
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var curTime = _gameTiming.CurTime;
        var enumerator = EntityQueryEnumerator<TracerComponent, TransformComponent>();
        while (enumerator.MoveNext(out var uid, out var tracerComponent, out var transformComponent))
        {
            if (curTime > tracerComponent.EndTime)
            {
                RemCompDeferred<TracerComponent>(uid);
                continue;
            }

            if (!IsDrawn(uid))
            {
                tracerComponent.PositionHistory.Clear();
                continue;
            }

            if (transformComponent.MapID != tracerComponent.Map)
            {
                tracerComponent.PositionHistory.Clear();
                tracerComponent.Map = transformComponent.MapID;
            }

            var positionHistory = tracerComponent.PositionHistory;
            var position = _transformSystem.GetWorldPosition(transformComponent);
            if (positionHistory.Count > 0 && positionHistory[^1] == position)
                continue;

            positionHistory.Add(position);
            while (positionHistory.Count > 2 && GetLength(positionHistory) > tracerComponent.Length)
                positionHistory.RemoveAt(0);
        }
    }

    private static float GetLength(List<Vector2> positions)
    {
        var length = 0f;
        for (var i = 1; i < positions.Count; i++)
            length += Vector2.Distance(positions[i - 1], positions[i]);

        return length;
    }
}
