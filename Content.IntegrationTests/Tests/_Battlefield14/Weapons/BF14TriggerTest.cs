#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Trigger.Components;
using Content.Shared.Trigger.Components.Triggers;
using Content.Shared.Trigger.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Battlefield14.Weapons;

/// <summary>
///     The shape the porter gives BF14's keyless triggers on KS14's keyed ones: using a grenade only starts its fuse,
///         the fuse ends with no key so every effect goes off, a fragmentation grenade answers that keyless trigger,
///         and <see cref="TriggerOnCollideComponent.Blacklist"/> keeps a rocket from going off on what it lists.
/// </summary>
[TestFixture]
public sealed class BF14TriggerTest : GameTest
{
    public override PoolSettings PoolSettings => PsDisconnected;

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: BF14TestFuseGrenade
  components:
  - type: TriggerOnUse
    keyOut: use
  - type: TimerTrigger
    keysIn:
    - use
    keyOut: null
    delay: 1
  - type: DeleteOnTrigger

- type: entity
  id: BF14TestFragGrenade
  components:
  - type: ContainerContainer
    containers:
      cluster-payload: !type:Container
  - type: ProjectileGrenade
    fillPrototype: BF14TestTriggerFragment
    capacity: 2

- type: entity
  parent: BaseBullet
  id: BF14TestTriggerFragment

- type: entity
  id: BF14TestDecayingMine
  components:
  - type: TriggerOnSpawn
    keyOut: spawn
  - type: TimerTrigger
    keysIn:
    - spawn
    keyOut: null
    delay: 0.5
  - type: SpawnOnTrigger
    proto: BF14TestArmedMine
  - type: DeleteOnTrigger

- type: entity
  id: BF14TestArmedMine
  components:
  - type: TriggerOnSpawn
    keyOut: spawn
  - type: TimerTrigger
    keysIn:
    - spawn
    keyOut: null
    delay: 60

- type: entity
  id: BF14TestAntiTankRocket
  components:
  - type: TriggerOnCollide
    fixtureID: projectile
    blacklist:
      components:
      - HumanoidProfile
";

    private readonly EntProtoId _fuseGrenade = "BF14TestFuseGrenade";
    private readonly EntProtoId _fragGrenade = "BF14TestFragGrenade";
    private readonly EntProtoId _decayingMine = "BF14TestDecayingMine";
    private readonly EntProtoId _armedMine = "BF14TestArmedMine";
    private readonly EntProtoId _rocket = "BF14TestAntiTankRocket";
    private static readonly EntProtoId Person = "MobHuman";
    private static readonly EntProtoId Wall = "WallSolid";

    [Test]
    public async Task UseStartsTheFuseAndItsEndSetsOffEveryEffect()
    {
        var testMap = await Pair.CreateTestMap();
        var trigger = Server.System<TriggerSystem>();

        EntityUid grenade = default;
        await Server.WaitPost(() =>
        {
            grenade = SSpawnAtPosition(_fuseGrenade, new EntityCoordinates(testMap.MapUid, 0, 0));
            Assert.That(SEntMan.GetComponent<TimerTriggerComponent>(grenade).KeyOut, Is.Null, "keyOut: null did not load as no key");
            trigger.Trigger(grenade, null, "use");
        });

        await RunSeconds(0.5f);
        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.Deleted(grenade), Is.False, "the grenade went off on use, not when its fuse ended");
            Assert.That(SEntMan.HasComponent<ActiveTimerTriggerComponent>(grenade), Is.True, "using the grenade did not start its fuse");
        });

        await RunSeconds(1f);
        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.Deleted(grenade), Is.True, "the fuse ended without setting off DeleteOnTrigger"));
    }

    /// <summary>
    ///     A timer whose end spawns something with its own timer from spawn, as BF14's PFM-1 item and Molotov do.
    ///         The new timer starts while the timers are being gone through.
    /// </summary>
    [Test]
    public async Task TimerCanSpawnAnotherRunningTimer()
    {
        var testMap = await Pair.CreateTestMap();

        await Server.WaitPost(() => SSpawnAtPosition(_decayingMine, new EntityCoordinates(testMap.MapUid, 0, 0)));
        await RunSeconds(1f);

        await Server.WaitAssertion(() =>
        {
            var armed = SEntMan.EntityQuery<TimerTriggerComponent>()
                .Where(t => SEntMan.GetComponent<MetaDataComponent>(t.Owner).EntityPrototype?.ID == _armedMine.Id)
                .ToList();
            Assert.That(armed, Has.Count.EqualTo(1), "the decaying mine did not leave one armed mine");
            Assert.That(SEntMan.HasComponent<ActiveTimerTriggerComponent>(armed[0].Owner), Is.True,
                "the armed mine's own timer did not start on spawn");
        });
    }

    [Test]
    public async Task FragGrenadeAnswersAKeylessTrigger()
    {
        var testMap = await Pair.CreateTestMap();
        var trigger = Server.System<TriggerSystem>();

        await Server.WaitAssertion(() =>
        {
            var origin = new EntityCoordinates(testMap.MapUid, 0, 0);
            var other = SSpawnAtPosition(_fragGrenade, origin);
            Assert.That(trigger.Trigger(other, null, "use"), Is.False, "a fragmentation grenade went off on a key it does not take");

            var grenade = SSpawnAtPosition(_fragGrenade, origin);
            Assert.That(trigger.Trigger(grenade, null, null), Is.True, "a fragmentation grenade ignored a keyless trigger");
        });
    }

    [Test]
    public async Task RocketDoesNotGoOffOnWhatItsBlacklistLists()
    {
        var testMap = await Pair.CreateTestMap();
        var trigger = Server.System<TriggerSystem>();

        await Server.WaitAssertion(() =>
        {
            var origin = new EntityCoordinates(testMap.MapUid, 0, 0);
            var person = SSpawnAtPosition(Person, origin);
            var wall = SSpawnAtPosition(Wall, new EntityCoordinates(testMap.MapUid, 3, 0));

            var rocket = SSpawnAtPosition(_rocket, origin);
            Assert.That(trigger.TryCollideTrigger(rocket, person, "projectile", HardFixture(person)), Is.False,
                "the rocket went off on a person, which its blacklist lists");
            Assert.That(trigger.TryCollideTrigger(rocket, wall, "projectile", HardFixture(wall)), Is.True,
                "the rocket did not go off on a wall");
        });
    }

    private Fixture HardFixture(EntityUid uid)
    {
        return SEntMan.GetComponent<FixturesComponent>(uid).Fixtures.Values.First(f => f.Hard);
    }
}
