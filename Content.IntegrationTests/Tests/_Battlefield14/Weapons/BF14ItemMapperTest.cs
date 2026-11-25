#nullable enable
using System.Collections.Generic;
using Content.IntegrationTests.Fixtures;
using Content.Shared._ES.Storage.ItemMapper.Components;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Battlefield14.Weapons;

/// <summary>
///     ES's item mapper, as BF14's guns use it for their suppressors, bayonets and stocks: a layer shows the state of
///         the first mapping whose container holds a match, and is hidden when none does.
/// </summary>
[TestFixture]
public sealed class BF14ItemMapperTest : GameTest
{
    public override PoolSettings PoolSettings => PsDisconnected;

    [TestPrototypes]
    private const string Prototypes = @"
- type: Tag
  id: BF14TestMapperSuppressor

- type: entity
  id: BF14TestMapperGun
  components:
  - type: Appearance
  - type: ContainerContainer
    containers:
      attachment_barrel: !type:ContainerSlot
  - type: ESItemMapper
    mappings:
      barrelAttachment:
      - containerId: attachment_barrel
        state: supp
        whitelist:
          tags:
          - BF14TestMapperSuppressor

- type: entity
  id: BF14TestMapperSuppressor
  components:
  - type: Tag
    tags:
    - BF14TestMapperSuppressor

- type: entity
  id: BF14TestMapperOther
";

    private readonly EntProtoId _gun = "BF14TestMapperGun";
    private readonly EntProtoId _suppressor = "BF14TestMapperSuppressor";
    private readonly EntProtoId _other = "BF14TestMapperOther";
    private const string Slot = "attachment_barrel";
    private const string Layer = "barrelAttachment";

    [Test]
    public async Task LayerFollowsWhatTheSlotHolds()
    {
        var testMap = await Pair.CreateTestMap();
        var container = Server.System<SharedContainerSystem>();
        var appearance = Server.System<SharedAppearanceSystem>();

        await Server.WaitAssertion(() =>
        {
            var origin = new EntityCoordinates(testMap.MapUid, 0, 0);
            var gun = SSpawnAtPosition(_gun, origin);
            var slot = container.GetContainer(gun, Slot);

            Assert.That(LayerState(appearance, gun), Is.Null, "the layer showed with the slot empty");

            var other = SSpawnAtPosition(_other, origin);
            Assert.That(container.Insert(other, slot), Is.True);
            Assert.That(LayerState(appearance, gun), Is.Null, "the layer showed for an item its whitelist does not take");

            Assert.That(container.Remove(other, slot), Is.True);
            var suppressor = SSpawnAtPosition(_suppressor, origin);
            Assert.That(container.Insert(suppressor, slot), Is.True);
            Assert.That(LayerState(appearance, gun), Is.EqualTo("supp"), "the layer did not show the suppressor");

            Assert.That(container.Remove(suppressor, slot), Is.True);
            Assert.That(LayerState(appearance, gun), Is.Null, "the layer stayed after the suppressor came off");
        });
    }

    private static string? LayerState(SharedAppearanceSystem appearance, EntityUid gun)
    {
        Assert.That(appearance.TryGetData(gun, ESItemMapperVisuals.Layers, out Dictionary<string, string?>? layers), Is.True,
            "the item mapper set no appearance data");
        Assert.That(layers!.ContainsKey(Layer), Is.True);
        return layers[Layer];
    }
}
