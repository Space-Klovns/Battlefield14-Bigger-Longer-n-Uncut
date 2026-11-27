#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.IntegrationTests.Fixtures;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool;

namespace Content.IntegrationTests.Tests._KS14.Chemistry;

/// <summary>
///     An injector whose solution can't be found does nothing when used, and says nothing either.
///         A SolutionContainerManager block leaves an entity that way, because the compatibility shim removes the manager it made.
/// </summary>
[TestFixture]
[TestOf(typeof(InjectorSystem))]
public sealed class KsInjectorSolutionTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    private static readonly EntProtoId Human = "MobHuman";
    private static readonly EntProtoId Syrette = "BF14SyretteBicaridine";
    private static readonly ProtoId<ReagentPrototype> Reagent = "Bicaridine";

    [Test]
    public async Task EveryInjectorFindsItsSolution()
    {
        var testMap = await Pair.CreateTestMap();
        var coordinates = new EntityCoordinates(testMap.Grid, 0.5f, 0.5f);
        var factory = SEntMan.ComponentFactory;
        var injectorIds = SProtoMan.EnumeratePrototypes<EntityPrototype>()
            .Where(proto => !proto.Abstract && !Pair.IsTestPrototype(proto) && proto.TryGetComponent<InjectorComponent>(out _, factory))
            .Select(proto => proto.ID)
            .ToList();
        var spawned = new List<EntityUid>();

        await Server.WaitPost(() =>
        {
            foreach (var id in injectorIds)
                spawned.Add(SEntMan.SpawnEntity(id, coordinates));
        });
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            var solutionSystem = SEntMan.System<SharedSolutionContainerSystem>();
            Assert.Multiple(() =>
            {
                for (var i = 0; i < spawned.Count; i++)
                {
                    var injectorComponent = SEntMan.GetComponent<InjectorComponent>(spawned[i]);
                    Assert.That(solutionSystem.TryGetSolution(spawned[i], injectorComponent.SolutionName, out _, out _), Is.True,
                        $"{injectorIds[i]} has no solution named {injectorComponent.SolutionName}, so it can't inject or draw.");
                }
            });

            foreach (var uid in spawned)
                SEntMan.DeleteEntity(uid);
        });
    }

    /// <summary>
    ///     The client must find a solution the server made from SolutionManager's solutionData before the server ever changes it.
    ///         Its item status and predicted inject sound read the client's copy, so without it they stay blank and silent until the first inject.
    /// </summary>
    [Test]
    public async Task TheClientFindsASolutionMadeFromSolutionData()
    {
        var testMap = await Pair.CreateTestMap();
        var coordinates = new EntityCoordinates(testMap.Grid, 0.5f, 0.5f);
        EntityUid serverUid = default;

        await Server.WaitPost(() => serverUid = SEntMan.SpawnEntity("BF14HyposprayFilledBicaridine", coordinates));
        await Pair.RunTicksSync(10);

        await Client.WaitAssertion(() =>
        {
            var clientUid = ToClientUid(serverUid);
            Assert.That(CEntMan.System<SharedSolutionContainerSystem>().TryGetSolution(clientUid, "hypospray", out _, out var solution), Is.True,
                "The client can't find the hypospray's solution.");
            Assert.That((float)solution!.GetTotalPrototypeQuantity(Reagent.Id), Is.GreaterThan(0f));
        });
    }

    [Test]
    public async Task ASyretteInjectsItsDoseWhenUsedInHand()
    {
        var testMap = await Pair.CreateTestMap();
        var coordinates = new EntityCoordinates(testMap.Grid, 0.5f, 0.5f);
        EntityUid userUid = default;
        EntityUid syretteUid = default;
        var dose = 0f;

        await Server.WaitAssertion(() =>
        {
            var solutionSystem = SEntMan.System<SharedSolutionContainerSystem>();
            userUid = SEntMan.SpawnEntity(Human, coordinates);
            Server.PlayerMan.SetAttachedEntity(ServerSession!, userUid);
            syretteUid = SEntMan.SpawnEntity(Syrette, coordinates);
            var injectorComponent = SEntMan.GetComponent<InjectorComponent>(syretteUid);
            Assert.That(solutionSystem.TryGetSolution(syretteUid, injectorComponent.SolutionName, out _, out var syretteSolution), Is.True,
                "precondition: the syrette's solution can be found");
            dose = (float)syretteSolution!.GetTotalPrototypeQuantity(Reagent.Id);
            Assert.That(dose, Is.GreaterThan(0f), $"precondition: the syrette holds {Reagent}");
            Assert.That(SEntMan.System<SharedHandsSystem>().TryPickupAnyHand(userUid, syretteUid), Is.True);
        });
        await Pair.RunTicksSync(5);

        await Server.WaitPost(() => SEntMan.System<SharedInteractionSystem>().UseInHandInteraction(userUid, syretteUid));
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.System<SharedSolutionContainerSystem>().TryGetInjectableSolution(userUid, out _, out var bloodstream), Is.True);
            Assert.That((float)bloodstream!.GetTotalPrototypeQuantity(Reagent.Id), Is.GreaterThan(0f),
                "Using a syrette in hand must put its reagent in the user's bloodstream.");
        });
    }
}
