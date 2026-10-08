using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureJobs.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Props.Commands;
using TRPG.Application.Props.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.Arrivals;

public class ExecuteJourneyArrivalsCommand
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<JourneyCompleted> Arrivals { get; init; }
}

internal class ExecuteJourneyArrivalsCommandHandler(
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    ICommandHandler<ExecuteCreatureJobCommand> executeCreatureJob,
    IQueryHandler<
        GetCreaturesAtLocationQuery,
        IReadOnlyCollection<CreatureResult>
    > getCreaturesAtLocation,
    IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>> getLocationsByIds,
    IQueryHandler<
        GetWorkstationsByLocationIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Workstation>>
    > getWorkstationsByLocationIds,
    ICommandHandler<SetWorkstationOccupantsCommand> setWorkstationOccupants
) : ICommandHandler<ExecuteJourneyArrivalsCommand>
{
    public async Task Handle(
        ExecuteJourneyArrivalsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.Arrivals.Count == 0)
        {
            return;
        }

        var creaturesById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery
            {
                Ids = [.. command.Arrivals.Select(arrival => arrival.CreatureId)],
            },
            cancellationToken
        );

        await ExecuteJobs(command.Arrivals, creaturesById, cancellationToken);
        await AssignWorkstations(command, cancellationToken);
    }

    private async Task ExecuteJobs(
        IReadOnlyCollection<JourneyCompleted> arrivals,
        IReadOnlyDictionary<Guid, Creature> creaturesById,
        CancellationToken cancellationToken
    )
    {
        foreach (var arrival in arrivals.OrderBy(arrival => arrival.At))
        {
            if (creaturesById.GetValueOrDefault(arrival.CreatureId) is not { } creature)
            {
                continue;
            }

            await executeCreatureJob.Handle(
                new ExecuteCreatureJobCommand
                {
                    CreatureId = creature.Id,
                    CurrentLocationId = creature.LocationId,
                    CurrentCondition = creature.Condition,
                    CurrentPosture = creature.Posture,
                    CreatureJobAction = arrival.Action,
                    JobLocationId = arrival.LocationId,
                },
                cancellationToken
            );
        }
    }

    private async Task AssignWorkstations(
        ExecuteJourneyArrivalsCommand command,
        CancellationToken cancellationToken
    )
    {
        var workLocationIds = command
            .Arrivals.Where(arrival => arrival.Action == CreatureJobAction.Work)
            .Select(arrival => arrival.LocationId)
            .Distinct()
            .ToArray();
        if (workLocationIds.Length == 0)
        {
            return;
        }

        var locationsById = await getLocationsByIds.Handle(
            new GetLocationsByIdsQuery { Ids = workLocationIds },
            cancellationToken
        );
        var roomLocationIds = workLocationIds
            .Where(id => locationsById.GetValueOrDefault(id)?.RoomId != null)
            .ToArray();
        if (roomLocationIds.Length == 0)
        {
            return;
        }

        var workstationsByLocationId = await getWorkstationsByLocationIds.Handle(
            new GetWorkstationsByLocationIdsQuery { LocationIds = roomLocationIds },
            cancellationToken
        );

        var occupantIdsByWorkstationId = new Dictionary<Guid, Guid?>();
        foreach (var locationId in roomLocationIds)
        {
            var workerIds = await FindWorkerIds(command.WorldId, locationId, cancellationToken);
            var workstations = workstationsByLocationId.GetValueOrDefault(
                locationId,
                (IReadOnlyList<Workstation>)[]
            );
            AssignInOrder(workstations, workerIds, occupantIdsByWorkstationId);
        }

        await setWorkstationOccupants.Handle(
            new SetWorkstationOccupantsCommand
            {
                OccupantIdsByWorkstationId = occupantIdsByWorkstationId,
            },
            cancellationToken
        );
    }

    private async Task<Queue<Guid>> FindWorkerIds(
        Guid worldId,
        Guid locationId,
        CancellationToken cancellationToken
    )
    {
        var present = await getCreaturesAtLocation.Handle(
            new GetCreaturesAtLocationQuery
            {
                WorldId = worldId,
                LocationId = locationId,
                IncludeDead = false,
            },
            cancellationToken
        );

        return new Queue<Guid>(
            present
                .Where(creature => creature.Activity == CreatureActivity.Working)
                .Select(creature => creature.Id)
                .Order()
        );
    }

    private static void AssignInOrder(
        IReadOnlyList<Workstation> workstations,
        Queue<Guid> workerIds,
        Dictionary<Guid, Guid?> occupantIdsByWorkstationId
    )
    {
        var counters = workstations.Where(station =>
            station.WorkstationType == WorkstationType.Trade
        );
        var productionStations = workstations.Where(station =>
            station.WorkstationType != WorkstationType.Trade
        );

        foreach (var station in counters.Concat(productionStations))
        {
            occupantIdsByWorkstationId[station.Id] =
                workerIds.Count > 0 ? workerIds.Dequeue() : null;
        }
    }
}
