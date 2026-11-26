#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._KS14.PredictedSpawning;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Mono.ArmorPiercing;
using Content.Shared.Damage.Systems;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Battlefield14.Projectiles;

/// <summary>
///     Battlefield14's wallbangs (Mono's <see cref="ArmorPiercingComponent"/>): a round at least as thick as a wall's
///         <see cref="ArmorThicknessComponent"/> flies on through it, undamaged, and one thinner stops at it. Both for
///         the server's copy and for the shooter's kept copy, so the shooter sees the shot go where it really goes.
/// </summary>
[TestOf(typeof(ArmorPiercingSystem))]
public sealed class BF14WallbangTest : InteractionTest
{
    protected override string PlayerPrototype => "MobHuman"; // combat mode and hands, to fire a gun

    private const string PiercingRound = "BF14TestPiercingRound";
    private const string WeakRound = "BF14TestWeakRound";
    private static readonly EntProtoId WallSolid = "WallSolid"; // BaseWall, thickness 3
    private const float WallX = 4.5f;

    [TestPrototypes]
    private const string Prototypes = $@"
- type: entity
  parent: BulletPistol
  id: {PiercingRound}
  components:
  - type: ArmorPiercing
    piercingThickness: 3

- type: entity
  parent: BulletPistol
  id: {WeakRound}
  components:
  - type: ArmorPiercing
    piercingThickness: 2

- type: entity
  id: BF14TestPiercingCartridge
  components:
  - type: CartridgeAmmo
    proto: {PiercingRound}
    deleteOnSpawn: true

- type: entity
  id: BF14TestWeakCartridge
  components:
  - type: CartridgeAmmo
    proto: {WeakRound}
    deleteOnSpawn: true

- type: entity
  abstract: true
  id: BF14TestWallbangBaseGun
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
  parent: BF14TestWallbangBaseGun
  id: BF14TestPiercingGun
  components:
  - type: BasicEntityAmmoProvider
    proto: BF14TestPiercingCartridge

- type: entity
  parent: BF14TestWallbangBaseGun
  id: BF14TestWeakGun
  components:
  - type: BasicEntityAmmoProvider
    proto: BF14TestWeakCartridge
";

    [Test]
    public async Task RoundPassesWallsNoThickerThanItself([Values] bool piercing)
    {
        var round = piercing ? PiercingRound : WeakRound;

        var clientConfigurationManager = Client.ResolveDependency<IConfigurationManager>();
        await Client.WaitPost(() => clientConfigurationManager.SetCVar(CVars.NetPredictTickBias, 4));

        // A row of real walls on the station-like grid the shooter stands on, across the line of fire.
        EntityUid wallUid = default;
        await Server.WaitPost(() =>
        {
            var plating = new Tile(TileMan[Plating].TileId);
            for (var tileX = -2; tileX <= 12; tileX++)
            {
                for (var tileY = -3; tileY <= 3; tileY++)
                    MapSystem.SetTile(MapData.Grid, new Vector2i(tileX, tileY), plating);
            }

            for (var tileY = -3; tileY <= 3; tileY++)
            {
                var uid = SEntMan.SpawnEntity(WallSolid, new EntityCoordinates(MapData.Grid, new Vector2(WallX, tileY + 0.5f)));
                if (tileY == 0)
                    wallUid = uid;
            }
        });
        await RunTicks(5);

        var gunNetEntity = await PlaceInHands(piercing ? "BF14TestPiercingGun" : "BF14TestWeakGun");
        await SetCombatMode(true);
        await RunSeconds(2f); // the pickup cooldown

        var wallX = 0f;
        await Server.WaitPost(() => wallX = SEntMan.System<SharedTransformSystem>().GetWorldPosition(wallUid).X);

        var targetCoordinates = SEntMan.GetNetCoordinates(new EntityCoordinates(MapData.Grid, new Vector2(40f, 0.5f)));
        await Client.WaitPost(() => CEntMan.RaisePredictiveEvent(new RequestShootEvent { Gun = gunNetEntity, Coordinates = targetCoordinates }));

        float serverFurthest = float.MinValue, keptFurthest = float.MinValue;
        for (var tick = 0; tick < 30; tick++)
        {
            await RunTicks(1);

            await Server.WaitPost(() =>
            {
                foreach (var uid in Find(SEntMan, round))
                    serverFurthest = MathF.Max(serverFurthest, SEntMan.System<SharedTransformSystem>().GetWorldPosition(uid).X);
            });
            await Client.WaitPost(() =>
            {
                foreach (var uid in Find(CEntMan, round).Where(uid => CEntMan.HasComponent<KsKeptPredictedSpawnComponent>(uid)))
                    keptFurthest = MathF.Max(keptFurthest, CEntMan.System<SharedTransformSystem>().GetWorldPosition(uid).X);
            });
        }

        var wallDamage = 0f;
        await Server.WaitPost(() => wallDamage = SEntMan.System<DamageableSystem>().GetTotalDamage((wallUid, null)).Float());

        Assert.Multiple(() =>
        {
            Assert.That(serverFurthest, Is.GreaterThan(float.MinValue), "the server never fired, so this proves nothing");
            Assert.That(keptFurthest, Is.GreaterThan(float.MinValue), "the shooter kept no copy, so this proves nothing");
            if (piercing)
            {
                Assert.That(serverFurthest, Is.GreaterThan(wallX + 2f), "the server's round stopped at a wall it is thick enough to pass");
                Assert.That(keptFurthest, Is.GreaterThan(wallX + 2f), "the shooter's kept round stopped at a wall the real one passed");
                Assert.That(wallDamage, Is.Zero, "the wall the round passed through was damaged");
            }
            else
            {
                Assert.That(serverFurthest, Is.LessThan(wallX), "the server's round passed a wall thicker than itself");
                Assert.That(keptFurthest, Is.LessThan(wallX), "the shooter's kept round passed a wall thicker than itself");
            }
        });
    }

    private static List<EntityUid> Find(IEntityManager entityManager, string prototype)
    {
        var found = new List<EntityUid>();
        var enumerator = entityManager.AllEntityQueryEnumerator<MetaDataComponent>();
        while (enumerator.MoveNext(out var uid, out var metaDataComponent))
        {
            if (metaDataComponent.EntityPrototype?.ID == prototype)
                found.Add(uid);
        }

        return found;
    }
}
