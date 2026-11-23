#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Client._KS14.PredictedSpawning;
using Content.IntegrationTests.Tests.Interaction;
using Content.Server._Battlefield14.Projectiles;
using Content.Shared._Battlefield14.Projectiles;
using Content.Shared.Damage;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Client.GameObjects;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Battlefield14.Projectiles;

/// <summary>
///     Battlefield14's projectiles that turn into others (<see cref="KsSubmunitionProjectileComponent"/>,
///         <see cref="BF14HeFragProjectileComponent"/>), fired by the client through the real shoot request, as KS14's
///         gun prediction runs them: the shooter's client keeps its own copy of the round, and only the server spawns
///         what it turns into, which everyone, the shooter too, then sees.
/// </summary>
[TestOf(typeof(SharedKsSubmunitionProjectileSystem))]
[TestOf(typeof(BF14HeFragProjectileSystem))]
public sealed class BF14ProjectilePredictionTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman"; // combat mode and hands, to fire a gun

    private const string TimedRound = "BF14TestSubmunitionRoundTimed";
    private const string DistanceRound = "BF14TestSubmunitionRoundDistance";
    private const string Submunition = "BF14TestSubmunition";
    private const string TimedGun = "BF14TestSubmunitionGunTimed";
    private const string DistanceGun = "BF14TestSubmunitionGunDistance";
    private const string FragRound = "BF14TestFragRound";
    private const string Fragment = "BF14TestFragment";
    private const string FragGun = "BF14TestFragGun";
    private const string TestWall = "BF14TestProjectileWall";
    private const int SubmunitionCount = 3;
    private const int FragmentCount = 6;

    [TestPrototypes]
    private const string Prototypes = $@"
- type: entity
  parent: BulletPistol
  id: {TimedRound}
  components:
  - type: KsSubmunitionProjectile
    submunitionPrototype: {Submunition}
    submunitionDelay: 0.3
    submunitionCount: 3 # SubmunitionCount
    spreadAngle: 30
    blacklist:
      components:
      - Damageable

- type: entity
  parent: BulletPistol
  id: {DistanceRound}
  components:
  - type: KsSubmunitionProjectile
    submunitionPrototype: {Submunition}
    distanceDelay: 5
    submunitionCount: 3 # SubmunitionCount
    spreadAngle: 30

- type: entity
  parent: BulletPistol
  id: {Submunition}

- type: entity
  parent: BulletPistol
  id: {FragRound}
  components:
  - type: BF14HeFragProjectile
    fillPrototype: {Fragment}
    capacity: 6 # FragmentCount

- type: entity
  parent: BulletPistol
  id: {Fragment}

- type: entity
  id: BF14TestSubmunitionCartridgeTimed
  components:
  - type: CartridgeAmmo
    proto: {TimedRound}
    deleteOnSpawn: true

- type: entity
  id: BF14TestSubmunitionCartridgeDistance
  components:
  - type: CartridgeAmmo
    proto: {DistanceRound}
    deleteOnSpawn: true

- type: entity
  id: BF14TestFragCartridge
  components:
  - type: CartridgeAmmo
    proto: {FragRound}
    deleteOnSpawn: true

- type: entity
  abstract: true
  id: BF14TestProjectileBaseGun
  components:
  - type: Item
  - type: Gun
    fireRate: 2
    minAngle: 0
    maxAngle: 0
    selectedMode: SemiAuto
    availableModes:
    - SemiAuto

- type: entity
  parent: BF14TestProjectileBaseGun
  id: {TimedGun}
  components:
  - type: BasicEntityAmmoProvider
    proto: BF14TestSubmunitionCartridgeTimed
    capacity: 10
    count: 10

- type: entity
  parent: BF14TestProjectileBaseGun
  id: {DistanceGun}
  components:
  - type: BasicEntityAmmoProvider
    proto: BF14TestSubmunitionCartridgeDistance
    capacity: 10
    count: 10

- type: entity
  parent: BF14TestProjectileBaseGun
  id: {FragGun}
  components:
  - type: BasicEntityAmmoProvider
    proto: BF14TestFragCartridge
    capacity: 10
    count: 10

- type: entity
  id: {TestWall}
  components:
  - type: Physics
    bodyType: Static
  - type: Fixtures
    fixtures:
      fix1:
        shape:
          !type:PhysShapeAabb
          bounds: ""-0.5,-0.5,0.5,0.5""
        hard: true
        layer:
        - WallLayer
        mask:
        - FullTileMask
  - type: Damageable
";

    /// <summary>
    ///     A round that splits in flight splits once, on the server, into exactly its submunitions, which reach the
    ///         shooter's client as the server's and are shown there. The shooter's kept copy of the round is gone by
    ///         the tick the server split: it splits itself, rather than waiting for the server's deletion to arrive.
    /// </summary>
    [Test]
    public async Task SubmunitionRoundSplitsOnceAndTheKeptCopyWithIt([Values(TimedGun, DistanceGun)] string gun)
    {
        var round = gun == TimedGun ? TimedRound : DistanceRound;
        var (shooterPosition, gunNetEntity) = await PrepareGun(gun);
        await Shoot(gunNetEntity, shooterPosition + new Vector2(40f, 0f));

        using var serverSubmunitions = WatchServerSpawns(Submunition);
        var clientSideSubmunitions = new HashSet<EntityUid>();
        var shownSubmunitions = new HashSet<EntityUid>();
        bool serverFired = false, keptSeen = false;
        int? serverSplitTick = null, keptGoneTick = null;

        for (var tick = 0; tick < 60; tick++)
        {
            await RunTicks(1);

            var currentTick = tick;
            await Server.WaitPost(() =>
            {
                var rounds = Find(SEntMan, round).Count;
                serverFired |= rounds > 0;
                if (serverFired && rounds == 0)
                    serverSplitTick ??= currentTick;
            });
            await Client.WaitPost(() =>
            {
                var keptRounds = 0;
                foreach (var uid in Find(CEntMan, round))
                {
                    if (CEntMan.HasComponent<KsKeptPredictedSpawnComponent>(uid))
                        keptRounds++;
                }

                keptSeen |= keptRounds > 0;
                if (keptSeen && keptRounds == 0)
                    keptGoneTick ??= currentTick;

                foreach (var uid in Find(CEntMan, Submunition))
                {
                    if (CEntMan.IsClientSide(uid))
                        clientSideSubmunitions.Add(uid);
                    else if (CEntMan.GetComponent<SpriteComponent>(uid).Visible)
                        shownSubmunitions.Add(uid);
                }
            });
        }

        Assert.Multiple(() =>
        {
            Assert.That(keptSeen, "the shooter's client never kept a copy of its round, so this proves nothing");
            Assert.That(serverSplitTick, Is.Not.Null, "the server's round never split");
            Assert.That(serverSubmunitions.Spawned, Has.Count.EqualTo(SubmunitionCount), "the server did not split into exactly its submunitions");
            Assert.That(clientSideSubmunitions, Is.Empty, "the shooter's client spawned submunitions of its own");
            Assert.That(shownSubmunitions, Has.Count.EqualTo(SubmunitionCount), "the shooter does not see the server's submunitions");
            Assert.That(keptGoneTick ?? int.MaxValue, Is.LessThanOrEqualTo(serverSplitTick ?? 0),
                $"the kept round outlived the server's split (tick {serverSplitTick}) until tick {keptGoneTick}");
        });
    }

    /// <summary>
    ///     A round that hits something on its blacklist before it splits never does: a dud. The control round, hit by
    ///         nothing, splits.
    /// </summary>
    [Test]
    public async Task SubmunitionRoundHitOnItsBlacklistNeverSplits()
    {
        EntityUid dudUid = default, controlUid = default;
        await Server.WaitPost(() =>
        {
            var wallUid = SEntMan.SpawnEntity(TestWall, SpaceCoordinates(10f));
            dudUid = SEntMan.SpawnEntity(TimedRound, SpaceCoordinates(0f));
            controlUid = SEntMan.SpawnEntity(TimedRound, SpaceCoordinates(4f));

            var hit = new ProjectileHitEvent(new DamageSpecifier(), wallUid);
            SEntMan.EventBus.RaiseLocalEvent(dudUid, ref hit);
        });

        await RunSeconds(1f);

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.EntityExists(controlUid), Is.False, "the control round never split, so this proves nothing");
                Assert.That(SEntMan.EntityExists(dudUid), "the round hit on its blacklist split anyway");
                Assert.That(Find(SEntMan, Submunition), Has.Count.EqualTo(SubmunitionCount), "not exactly the control round's submunitions");
            });
        });
    }

    /// <summary>
    ///     A fragmenting round fired at a wall bursts once, on the server, into exactly its fragments; the shooter's
    ///         client spawns none of its own, only its kept round, which is gone after the hit, and sees the server's.
    /// </summary>
    [Test]
    public async Task FragRoundBurstsOnceOnTheServer()
    {
        var (shooterPosition, gunNetEntity) = await PrepareGun(FragGun);
        await Server.WaitPost(() => SEntMan.SpawnEntity(TestWall, new MapCoordinates(shooterPosition + new Vector2(5f, 0f), MapData.MapId)));
        await RunTicks(5); // the wall reaches the client
        await Shoot(gunNetEntity, shooterPosition + new Vector2(40f, 0f));

        using var serverFragments = WatchServerSpawns(Fragment);
        var clientSideFragments = new HashSet<EntityUid>();
        var networkedFragments = new HashSet<EntityUid>();
        bool keptSeen = false, keptGone = false;

        for (var tick = 0; tick < 40; tick++)
        {
            await RunTicks(1);

            await Client.WaitPost(() =>
            {
                var keptRounds = 0;
                foreach (var uid in Find(CEntMan, FragRound))
                {
                    if (CEntMan.HasComponent<KsKeptPredictedSpawnComponent>(uid))
                        keptRounds++;
                }

                keptSeen |= keptRounds > 0;
                keptGone |= keptSeen && keptRounds == 0;

                foreach (var uid in Find(CEntMan, Fragment))
                {
                    if (CEntMan.IsClientSide(uid))
                        clientSideFragments.Add(uid);
                    else
                        networkedFragments.Add(uid);
                }
            });
        }

        Assert.Multiple(() =>
        {
            Assert.That(keptSeen, "the shooter's client never kept a copy of its round, so this proves nothing");
            Assert.That(keptGone, "the kept round outlived its hit");
            Assert.That(serverFragments.Spawned, Has.Count.EqualTo(FragmentCount), "the server did not burst into exactly its fragments");
            Assert.That(clientSideFragments, Is.Empty, "the shooter's client spawned fragments of its own");
            Assert.That(networkedFragments, Is.Not.Empty, "none of the server's fragments reached the shooter");
        });
    }

    /// <summary>
    ///     Off the 1x1 test grid, so nothing is in the way.
    /// </summary>
    private MapCoordinates SpaceCoordinates(float x) => new(MapData.MapCoords.Position + new Vector2(x, 20f), MapData.MapId);

    /// <summary>
    ///     Puts <paramref name="gun"/> in the player's hands, ready to fire, with the client re-predicting every tick as
    ///         a real one does, and returns where the player stands.
    /// </summary>
    private async Task<(Vector2 ShooterPosition, NetEntity Gun)> PrepareGun(string gun)
    {
        var clientConfigurationManager = Client.ResolveDependency<IConfigurationManager>();
        await Client.WaitPost(() => clientConfigurationManager.SetCVar(CVars.NetPredictTickBias, 4));

        var gunNetEntity = await PlaceInHands(gun);
        await SetCombatMode(true);
        await RunSeconds(2f); // the pickup cooldown

        var shooterPosition = Vector2.Zero;
        await Server.WaitPost(() => shooterPosition = SEntMan.System<SharedTransformSystem>().GetWorldPosition(SPlayer));
        return (shooterPosition, gunNetEntity);
    }

    private async Task Shoot(NetEntity gunNetEntity, Vector2 worldTarget)
    {
        var targetCoordinates = SEntMan.GetNetCoordinates(new EntityCoordinates(MapData.MapUid, worldTarget));
        await Client.WaitPost(() =>
        {
            CEntMan.RaisePredictiveEvent(new RequestShootEvent
            {
                Gun = gunNetEntity,
                Coordinates = targetCoordinates,
            });
        });
    }

    /// <summary>
    ///     Every entity of <paramref name="prototype"/> the server spawns from now until disposed: counted as it is made,
    ///         since a fragment spawned inside the wall it burst against is gone within the tick.
    /// </summary>
    private SpawnWatch WatchServerSpawns(string prototype) => new(SEntMan, prototype);

    private sealed class SpawnWatch : IDisposable
    {
        private readonly IEntityManager _entityManager;
        private readonly string _prototype;

        public readonly HashSet<EntityUid> Spawned = new();

        public SpawnWatch(IEntityManager entityManager, string prototype)
        {
            _entityManager = entityManager;
            _prototype = prototype;
            _entityManager.EntityInitialized += OnEntityInitialized;
        }

        private void OnEntityInitialized(Entity<MetaDataComponent> entity)
        {
            if (entity.Comp.EntityPrototype?.ID == _prototype)
                Spawned.Add(entity.Owner);
        }

        public void Dispose()
        {
            _entityManager.EntityInitialized -= OnEntityInitialized;
        }
    }

    private static List<EntityUid> Find(IEntityManager entityManager, string prototype)
    {
        var found = new List<EntityUid>();
        var enumerator = entityManager.AllEntityQueryEnumerator<ProjectileComponent, MetaDataComponent>();
        while (enumerator.MoveNext(out var uid, out _, out var metaDataComponent))
        {
            if (metaDataComponent.EntityPrototype?.ID == prototype)
                found.Add(uid);
        }

        return found;
    }
}
