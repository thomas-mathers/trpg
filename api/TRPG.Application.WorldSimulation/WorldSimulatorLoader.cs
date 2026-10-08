using Microsoft.Extensions.Options;
using TRPG.Application.Common.Navigation;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Routing.Queries;
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
    IQueryHandler<
        GetRouteTravelerCreatureIdsByWorldIdQuery,
        IReadOnlyCollection<Guid>
    > getRouteTravelerCreatureIds,
    IQueryHandler<
        GetRouteStopsByIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
    > getRouteStops,
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
        var routeTravelerIds = await getRouteTravelerCreatureIds.Handle(
            new GetRouteTravelerCreatureIdsByWorldIdQuery { WorldId = worldId },
            cancellationToken
        );
        var patrols = await LoadPatrols(graph, jobsByCreatureId, cancellationToken);

        return
        [
            .. creatures
                .Where(creature => !routeTravelerIds.Contains(creature.Id))
                .Select(creature => ToSeed(creature, jobsByCreatureId, patrols)),
        ];
    }

    private static SimCreatureSeed ToSeed(
        SimulatableCreature creature,
        IReadOnlyDictionary<Guid, IReadOnlyList<CreatureJob>> jobsByCreatureId,
        IReadOnlyDictionary<Guid, IReadOnlyList<RouteLeg>> patrols
    )
    {
        var jobs = jobsByCreatureId.GetValueOrDefault(creature.Id) ?? [];

        return new SimCreatureSeed(
            creature.Id,
            creature.LocationId,
            creature.MovementSpeed,
            jobs,
            creature.Profession != Profession.Guard,
            patrols
        );
    }

    private async Task<IReadOnlyDictionary<Guid, IReadOnlyList<RouteLeg>>> LoadPatrols(
        TravelGraph graph,
        IReadOnlyDictionary<Guid, IReadOnlyList<CreatureJob>> jobsByCreatureId,
        CancellationToken cancellationToken
    )
    {
        var routeIds = jobsByCreatureId
            .Values.SelectMany(jobs => jobs)
            .Select(job => job.RouteId)
            .OfType<Guid>()
            .Distinct()
            .ToArray();
        var stopsByRouteId = await getRouteStops.Handle(
            new GetRouteStopsByIdsQuery { RouteIds = routeIds },
            cancellationToken
        );

        return stopsByRouteId
            .Select(route => (route.Key, Legs: graph.BuildNodeCycle(route.Value)))
            .Where(route => route.Legs.Sum(leg => leg.Distance) > 0)
            .ToDictionary(route => route.Key, route => route.Legs);
    }
}
