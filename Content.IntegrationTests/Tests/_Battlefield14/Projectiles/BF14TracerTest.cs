#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._Emberfall.Weapons.Ranged;
using Content.Client._KS14.PredictedSpawning;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Emberfall.Weapons.Ranged;
using Content.Shared._KS14.PredictedSpawning;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Battlefield14.Projectiles;

/// <summary>
///     A tracer is client only, and drawn after the copy of a shot the client shows: the shooter's own kept copy, never
///         the hidden server copy beside it nor a re-predicted throwaway, so the shooter sees one tracer from the moment
///         they fire; and the server's copy for anyone else.
/// </summary>
[TestOf(typeof(TracerSystem))]
public sealed class BF14TracerTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman"; // combat mode and hands, to fire a gun

    private const string TracerRound = "BF14TestTracerRound";
    private const string TracerGun = "BF14TestTracerGun";

    [TestPrototypes]
    private const string Prototypes = $@"
- type: entity
  parent: BulletPistol
  id: {TracerRound}
  components:
  - type: Tracer
    color: red
    length: 3

- type: entity
  id: BF14TestTracerCartridge
  components:
  - type: CartridgeAmmo
    proto: {TracerRound}
    deleteOnSpawn: true

- type: entity
  id: {TracerGun}
  components:
  - type: Item
  - type: Gun
    fireRate: 2
    minAngle: 0
    maxAngle: 0
    selectedMode: SemiAuto
    availableModes:
    - SemiAuto
  - type: BasicEntityAmmoProvider
    proto: BF14TestTracerCartridge
    capacity: 10
    count: 10
";

    /// <summary>
    ///     The shooter's own shot: on every tick of its flight, exactly one copy draws a tracer, and it is the kept one.
    /// </summary>
    [Test]
    public async Task ShooterDrawsOneTracerFromTheKeptCopy()
    {
        var clientConfigurationManager = Client.ResolveDependency<IConfigurationManager>();
        await Client.WaitPost(() => clientConfigurationManager.SetCVar(CVars.NetPredictTickBias, 4)); // re-predicting, so throwaways exist

        var gunNetEntity = await PlaceInHands(TracerGun);
        await SetCombatMode(true);
        await RunSeconds(2f); // the pickup cooldown

        var shooterPosition = Vector2.Zero;
        await Server.WaitPost(() => shooterPosition = SEntMan.System<SharedTransformSystem>().GetWorldPosition(SPlayer));
        var targetCoordinates = SEntMan.GetNetCoordinates(new EntityCoordinates(MapData.MapUid, shooterPosition + new Vector2(40f, 0f)));
        await Client.WaitPost(() => CEntMan.RaisePredictiveEvent(new RequestShootEvent { Gun = gunNetEntity, Coordinates = targetCoordinates }));

        var sawServerCopy = false;
        for (var tick = 0; tick < 12; tick++)
        {
            await RunTicks(1);

            var currentTick = tick;
            await Client.WaitAssertion(() =>
            {
                var tracerSystem = CEntMan.System<TracerSystem>();
                var rounds = FindRounds();
                sawServerCopy |= rounds.Any(uid => !CEntMan.IsClientSide(uid));

                var drawn = rounds.Where(tracerSystem.IsDrawn).ToList();
                Assert.That(drawn, Has.Count.EqualTo(1), $"{drawn.Count} tracers drawn {currentTick + 1} ticks after the shot, of {rounds.Count} copies");
                Assert.That(CEntMan.HasComponent<KsKeptPredictedSpawnComponent>(drawn.Single()), "the tracer drawn is not the kept copy's");
            });
        }

        Assert.That(sawServerCopy, "the server's copy never reached the shooter, so this proves nothing");
    }

    /// <summary>
    ///     A shot nobody here predicted (an NPC's, or another player's): the server's copy draws its tracer.
    /// </summary>
    [Test]
    public async Task ObserverDrawsTheServersTracer()
    {
        EntityUid gunUid = default;
        await Server.WaitPost(() => gunUid = SEntMan.SpawnEntity(TracerGun, new MapCoordinates(MapData.MapCoords.Position + new Vector2(0f, 20f), MapData.MapId)));
        await RunTicks(5);

        await Server.WaitPost(() =>
        {
            var target = SEntMan.System<SharedTransformSystem>().ToCoordinates(new MapCoordinates(MapData.MapCoords.Position + new Vector2(40f, 20f), MapData.MapId));
            using (SEntMan.System<KsSharedPredictedSpawnSystem>().BeginUserPrediction(null))
                Assert.That(SEntMan.System<SharedGunSystem>().AttemptShoot(gunUid, (gunUid, SEntMan.GetComponent<GunComponent>(gunUid)), target));
        });
        await RunTicks(5);

        await Client.WaitAssertion(() =>
        {
            var rounds = FindRounds();
            Assert.That(rounds, Has.Count.EqualTo(1), "the server's round did not reach the client exactly once");
            Assert.That(CEntMan.System<TracerSystem>().IsDrawn(rounds.Single()), "the server's round draws no tracer for an observer");
        });
    }

    private List<EntityUid> FindRounds()
    {
        var rounds = new List<EntityUid>();
        var enumerator = CEntMan.AllEntityQueryEnumerator<TracerComponent, MetaDataComponent>();
        while (enumerator.MoveNext(out var uid, out _, out var metaDataComponent))
        {
            if (metaDataComponent.EntityPrototype?.ID == TracerRound)
                rounds.Add(uid);
        }

        return rounds;
    }
}
