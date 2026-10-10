using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Navigation;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Application.WorldSimulation.Poses;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation;

public sealed record LoadedWorldSimulation(WorldSimulator Simulator, CreaturePoseMapper PoseMapper);

public sealed class WorldSimulatorLoader(
    IQueryHandler<
        GetSimulatableCreaturesQuery,
        IReadOnlyCollection<SimulatableCreature>
    > getCreatures,
    IQueryHandler<
        GetCreatureJobsByCreatureIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<CreatureJob>>
    > getJobs,
    IQueryHandler<GetTravelGraphQuery, TravelGraph> getTravelGraph,
    IOptions<WorldClockOptions> clockOptions,
    IOptions<WorldSimulationOptions> simulationOptions,
    IRoutingDbContext routing
)
{
    public async Task<LoadedWorldSimulation> Load(
        Guid worldId,
        GameInstant now,
        CancellationToken cancellationToken = default
    )
    {
        var graph = await getTravelGraph.Handle(
            new GetTravelGraphQuery { WorldId = worldId },
            cancellationToken
        );

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

        return new LoadedWorldSimulation(simulator, new CreaturePoseMapper());
    }

    public async Task<SimCreatureSeed?> LoadSeed(
        Guid worldId,
        Guid creatureId,
        CancellationToken cancellationToken = default
    )
    {
        var graph = await getTravelGraph.Handle(
            new GetTravelGraphQuery { WorldId = worldId },
            cancellationToken
        );
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
        var journeysByCreatureId = await LoadJourneys(
            worldId,
            graph,
            creatures,
            jobsByCreatureId,
            cancellationToken
        );
        return
        [
            .. creatures.Select(creature =>
                ToSeed(
                    creature,
                    jobsByCreatureId,
                    journeysByCreatureId.GetValueOrDefault(creature.Id)
                )
            ),
        ];
    }

    private async Task<IReadOnlyDictionary<Guid, JourneyExecutionSeed>> LoadJourneys(
        Guid worldId,
        TravelGraph graph,
        IReadOnlyCollection<SimulatableCreature> creatures,
        IReadOnlyDictionary<Guid, IReadOnlyList<CreatureJob>> jobsByCreatureId,
        CancellationToken cancellationToken
    )
    {
        var creatureIds = creatures.Select(creature => creature.Id).ToArray();
        var members = await routing
            .JourneyMembers.AsNoTracking()
            .Where(member => creatureIds.AsEnumerable().Contains(member.CreatureId))
            .ToArrayAsync(cancellationToken);
        var journeyIds = members.Select(member => member.JourneyId).Distinct().ToArray();
        var journeys = await routing
            .Journeys.AsNoTracking()
            .Where(journey =>
                journey.WorldId == worldId
                && journeyIds.AsEnumerable().Contains(journey.Id)
                && (
                    journey.Status == JourneyStatus.Planned
                    || journey.Status == JourneyStatus.Traveling
                )
            )
            .ToArrayAsync(cancellationToken);
        var activeJourneyIds = journeys.Select(journey => journey.Id).ToArray();
        var legs = await routing
            .JourneyLegs.AsNoTracking()
            .Where(leg => activeJourneyIds.AsEnumerable().Contains(leg.JourneyId))
            .OrderBy(leg => leg.Index)
            .ToArrayAsync(cancellationToken);
        var creatureById = creatures.ToDictionary(creature => creature.Id);

        return members
            .Where(member => activeJourneyIds.Contains(member.JourneyId))
            .ToDictionary(
                member => member.CreatureId,
                member =>
                    ToJourneySeed(
                        journeys.Single(journey => journey.Id == member.JourneyId),
                        legs.Where(leg => leg.JourneyId == member.JourneyId).ToArray(),
                        members.Where(other => other.JourneyId == member.JourneyId),
                        creatureById,
                        jobsByCreatureId,
                        graph
                    )
            );
    }

    private static JourneyExecutionSeed ToJourneySeed(
        Journey journey,
        IReadOnlyList<JourneyLeg> legs,
        IEnumerable<JourneyMember> members,
        IReadOnlyDictionary<Guid, SimulatableCreature> creaturesById,
        IReadOnlyDictionary<Guid, IReadOnlyList<CreatureJob>> jobsByCreatureId,
        TravelGraph graph
    )
    {
        var journeyMembers = members
            .Select(member => creaturesById.GetValueOrDefault(member.CreatureId))
            .OfType<SimulatableCreature>()
            .ToArray();
        var destinationJob = journey.DestinationJobId is { } jobId
            ? journeyMembers
                .SelectMany(member => jobsByCreatureId.GetValueOrDefault(member.Id) ?? [])
                .SingleOrDefault(job => job.Id == jobId)
            : null;
        var locationsByNode = legs.SelectMany(leg => new[] { leg.FromNodeId, leg.ToNodeId })
            .Distinct()
            .ToDictionary(nodeId => nodeId, graph.LocationOf);

        return new JourneyExecutionSeed(
            journey.Id,
            journey.Status,
            journey.DepartureAt,
            journey.CheckpointLegIndex,
            journey.CheckpointLegProgressMeters,
            journey.CheckpointedAt,
            legs,
            destinationJob,
            locationsByNode
        );
    }

    private static SimCreatureSeed ToSeed(
        SimulatableCreature creature,
        IReadOnlyDictionary<Guid, IReadOnlyList<CreatureJob>> jobsByCreatureId,
        JourneyExecutionSeed? journey
    )
    {
        var jobs = jobsByCreatureId.GetValueOrDefault(creature.Id) ?? [];

        return new SimCreatureSeed(
            creature.Id,
            creature.LocationId,
            creature.MovementSpeed,
            jobs,
            creature.Profession != Profession.Guard,
            creature.CurrentTravelNodeId,
            journey,
            creature.IsEngaged
        );
    }
}
