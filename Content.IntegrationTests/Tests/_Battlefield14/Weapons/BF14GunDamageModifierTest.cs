#nullable enable
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.FixedPoint;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Battlefield14.Weapons;

/// <summary>
///     <see cref="GunComponent.DamageModifier"/>: every round a gun fires deals that many times its damage.
/// </summary>
[TestFixture]
public sealed class BF14GunDamageModifierTest : GameTest
{
    public override PoolSettings PoolSettings => PsDisconnected;

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  parent: WeaponRifleAk
  id: BF14TestRifleDoubleDamage
  components:
  - type: Gun
    damageModifier: 2
";

    private static readonly EntProtoId StockRifle = "WeaponRifleAk";
    private readonly EntProtoId _doubleDamageRifle = "BF14TestRifleDoubleDamage";
    private static readonly EntProtoId Cartridge = "CartridgeLightRifle";
    private const string ChamberSlot = "gun_chamber";

    [Test]
    public async Task RoundsDealTheGunsMultiple()
    {
        var stock = await FiredDamage(StockRifle);
        var doubled = await FiredDamage(_doubleDamageRifle);

        Assert.That(stock, Is.GreaterThan(FixedPoint2.Zero), "the stock rifle's round dealt no damage");
        Assert.That(doubled, Is.EqualTo(stock * 2), "the round did not deal twice the stock rifle's damage");
    }

    /// <summary>
    ///     Fires one round from a fresh rifle and returns the total damage the round would deal.
    /// </summary>
    private async Task<FixedPoint2> FiredDamage(EntProtoId rifleProto)
    {
        var testMap = await Pair.CreateTestMap();
        var gunSystem = Server.System<SharedGunSystem>();

        var itemSlots = Server.System<ItemSlotsSystem>();

        EntityUid rifle = default;
        await Server.WaitPost(() =>
        {
            var origin = new EntityCoordinates(testMap.MapUid, 0, 0);
            rifle = SSpawnAtPosition(rifleProto, origin);

            // a round in the chamber, with the bolt closed, as a freshly spawned rifle has none
            if (itemSlots.GetItemOrNull(rifle, ChamberSlot) is { } chambered)
                SEntMan.DeleteEntity(chambered);
            Assert.That(itemSlots.TryInsert(rifle, ChamberSlot, SSpawnAtPosition(Cartridge, origin), user: null), Is.True);
            gunSystem.SetBoltClosed(rifle, SEntMan.GetComponent<ChamberMagazineAmmoProviderComponent>(rifle), true);
        });

        await RunTicksSync(5);
        await Server.WaitAssertion(() =>
            Assert.That(gunSystem.AttemptShoot((rifle, SEntMan.GetComponent<GunComponent>(rifle))), Is.True, "the rifle did not fire"));
        await RunTicksSync(5);

        var total = FixedPoint2.Zero;
        await Server.WaitAssertion(() =>
        {
            var fired = SEntMan.EntityQuery<ProjectileComponent>().Where(p => p.Weapon == rifle).ToList();
            Assert.That(fired, Has.Count.EqualTo(1), "the rifle did not fire one round");
            total = fired[0].Damage.GetTotal();
        });

        return total;
    }
}
