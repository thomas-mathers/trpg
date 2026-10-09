using Microsoft.Extensions.Options;
using TRPG.Application.Common.Navigation;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Application.WorldSimulation.Poses;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation;

public sealed record LoadedWorldSimulation(
    WorldSimulator Simulator,
    CreaturePoseMapper PoseMapper,
    TravelGraph Graph
);

public sealed class WorldSimulatorLoader(
    IQueryHandler<
        GetSimulatableCreaturesQuery,
        IReadOnlyCollection<SimulatableCreature>
    > getCreatures,
    IQueryHandler<
        GetCreatureJobsByCreatureIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<CreatureJob>>
    > getJobs,
    IQueryHandler<GetTravelTopologyQuery, TravelTopology> getTopology,
    IOptions<WorldClockOptions> clockOptions,
    IOptions<WorldSimulationOptions> simulationOptions
)
{
    public async Task<LoadedWorldSimulation> Load(
        Guid worldId,
        GameInstant now,
        CancellationToken cancellationToken = default
    )
    {
        var topology = await getTopology.Handle(
            new GetTravelTopologyQuery { WorldId = worldId },
            cancellationToken
        );

        var graph = topology.ToGraph();
        var simulator = new WorldSimulator(
            graph,
            new WorldSimulatorOptions(
                clockOptions.Value.TimeScale,
                simulationOptions.Value.RouteSearchesPerTick,
                simulationOptions.Value.ArrivalStagger
            )
        );
        foreach (var seed in await LoadSeeds(worldId, graph, null, cancellationToken))
        {
            simulator.Add(seed, now);
        }

        return new LoadedWorldSimulation(
            simulator,
            new CreaturePoseMapper(
                PlacedConnector.Place(topology.LocationConnectors, topology.Nodes)
            ),
            graph
        );
    }

    public async Task<SimCreatureSeed?> LoadSeed(
        Guid worldId,
        Guid creatureId,
        TravelGraph graph,
        CancellationToken cancellationToken = default
    )
    {
        var seeds = await LoadSeeds(worldId, graph, [creatureId], cancellationToken);

        return seeds.SingleOrDefault();
    }

    private async Task<IReadOnlyList<SimCreatureSeed>> LoadSeeds(
        Guid worldId,
        TravelGraph graph,
        IReadOnlyCollection<Guid>? creatureIds,
        CancellationToken cancellationToken
    )
    {
        var creatures = await getCreatures.Handle(
            new GetSimulatableCreaturesQuery { WorldId = worldId, CreatureIds = creatureIds },
            cancellationToken
        );
        var jobsByCreatureId = await getJobs.Handle(
            new GetCreatureJobsByCreatureIdsQuery
            {
                CreatureIds = [.. creatures.Select(creature => creature.Id)],
            },
            cancellationToken
        );
        return [.. creatures.Select(creature => ToSeed(creature, jobsByCreatureId))];
    }

    private static SimCreatureSeed ToSeed(
        SimulatableCreature creature,
        IReadOnlyDictionary<Guid, IReadOnlyList<CreatureJob>> jobsByCreatureId
    )
    {
        var jobs = jobsByCreatureId.GetValueOrDefault(creature.Id) ?? [];

        return new SimCreatureSeed(
            creature.Id,
            creature.LocationId,
            creature.MovementSpeed,
            jobs,
            creature.Profession != Profession.Guard,
            creature.CurrentTravelNodeId
        );
    }
}
