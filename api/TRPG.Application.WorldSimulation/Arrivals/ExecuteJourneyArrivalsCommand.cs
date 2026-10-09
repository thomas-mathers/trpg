using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Navigation;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureJobs.Commands;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Props.Commands;
using TRPG.Application.Props.Queries;
using TRPG.Application.Routing.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Application.WorldSimulation.Movement;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.WorldSimulation.Arrivals;

public class ExecuteJourneyArrivalsCommand
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<JourneyCompleted> Arrivals { get; init; }
}

public sealed record JourneyArrivalResult(IReadOnlyCollection<Guid> ReroutedCreatureIds);

internal class ExecuteJourneyArrivalsCommandHandler(
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    IQueryHandler<GetPropByIdQuery, Prop?> getPropById,
    ICommandHandler<ExecuteCreatureJobCommand> executeCreatureJob,
    ICommandHandler<TryOccupySeatCommand, bool> tryOccupySeat,
    ICommandHandler<TryOccupyAssignedPropCommand, bool> tryOccupyAssignedProp,
    ICommandHandler<SetCreatureActivityCommand> setCreatureActivity,
    ICommandHandler<TryStartSittingCommand, bool> tryStartSitting,
    IQueryHandler<GetPropsByLocationIdQuery, IReadOnlyCollection<Prop>> getPropsByLocationId,
    IQueryHandler<GetTravelTopologyQuery, TravelTopology> getTravelTopology,
    IRoutingDbContext routing
) : ICommandHandler<ExecuteJourneyArrivalsCommand, JourneyArrivalResult>
{
    public async Task<JourneyArrivalResult> Handle(
        ExecuteJourneyArrivalsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var reroutedCreatureIds = new List<Guid>();
        var creaturesById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery
            {
                Ids = [.. command.Arrivals.Select(arrival => arrival.CreatureId)],
            },
            cancellationToken
        );
        foreach (var arrival in command.Arrivals.OrderBy(arrival => arrival.At))
        {
            if (creaturesById.GetValueOrDefault(arrival.CreatureId) is not { } creature)
            {
                continue;
            }
            if (arrival.DestinationPropId is not { } propId)
            {
                await CompleteStanding(arrival, creature, cancellationToken);
                continue;
            }
            var prop = await getPropById.Handle(
                new GetPropByIdQuery { Id = propId },
                cancellationToken
            );
            if (prop != null && prop.LocationId == arrival.LocationId)
            {
                if (await CompleteAtProp(arrival, creature, prop, cancellationToken))
                {
                    reroutedCreatureIds.Add(creature.Id);
                }
            }
        }
        return new JourneyArrivalResult(reroutedCreatureIds);
    }

    private async Task<bool> CompleteAtProp(
        JourneyCompleted arrival,
        Creature creature,
        Prop prop,
        CancellationToken cancellationToken
    )
    {
        if (arrival.Action == CreatureJobAction.Idle && prop is Seat seat)
        {
            if (
                await tryOccupySeat.Handle(
                    new TryOccupySeatCommand { SeatId = seat.Id, CreatureId = creature.Id },
                    cancellationToken
                )
            )
            {
                await setCreatureActivity.Handle(
                    new SetCreatureActivityCommand { CreatureIds = [creature.Id], Activity = null },
                    cancellationToken
                );
                await tryStartSitting.Handle(
                    new TryStartSittingCommand
                    {
                        CreatureId = creature.Id,
                        LocationId = arrival.LocationId,
                        X = seat.X,
                        Y = seat.Y,
                        Angle = seat.Angle,
                    },
                    cancellationToken
                );
                return false;
            }
            return await AppendSeatReroute(arrival, cancellationToken);
        }
        if (
            arrival.Action is CreatureJobAction.Sleep or CreatureJobAction.Work
            && await tryOccupyAssignedProp.Handle(
                new TryOccupyAssignedPropCommand
                {
                    PropId = prop.Id,
                    CreatureId = creature.Id,
                    Action = arrival.Action,
                },
                cancellationToken
            )
        )
        {
            await executeCreatureJob.Handle(JobCommand(arrival, creature), cancellationToken);
        }
        return false;
    }

    private async Task CompleteStanding(
        JourneyCompleted arrival,
        Creature creature,
        CancellationToken cancellationToken
    )
    {
        if (arrival.Action == CreatureJobAction.Idle)
        {
            await setCreatureActivity.Handle(
                new SetCreatureActivityCommand { CreatureIds = [creature.Id], Activity = null },
                cancellationToken
            );
            return;
        }
        await executeCreatureJob.Handle(JobCommand(arrival, creature), cancellationToken);
    }

    private async Task<bool> AppendSeatReroute(
        JourneyCompleted arrival,
        CancellationToken cancellationToken
    )
    {
        if (arrival.JourneyId is not { } journeyId || arrival.ArrivalNodeId is not { } originNodeId)
        {
            return false;
        }
        var props = await getPropsByLocationId.Handle(
            new GetPropsByLocationIdQuery { LocationId = arrival.LocationId },
            cancellationToken
        );
        if (props.Count == 0)
        {
            return false;
        }
        var topology = await getTravelTopology.Handle(
            new GetTravelTopologyQuery { WorldId = props.First().WorldId },
            cancellationToken
        );
        var graph = topology.ToGraph();
        var freeSeats = props
            .OfType<Seat>()
            .Where(seat => seat.OccupantId == null && seat.ApproachNodeId != null)
            .ToArray();
        var target = freeSeats
            .Select(seat => new RerouteTarget(
                seat.ApproachNodeId!.Value,
                seat.Id,
                graph.FindShortestPath(originNodeId, seat.ApproachNodeId.Value)
            ))
            .Where(target => target.Legs.Count > 0)
            .OrderBy(target => target.Legs.Sum(leg => leg.Distance))
            .FirstOrDefault();
        var approachNodeIds = props
            .Where(prop => prop.ApproachNodeId != null)
            .Select(prop => prop.ApproachNodeId!.Value)
            .ToHashSet();
        target ??= topology
            .Nodes.Where(node =>
                node.LocationId == arrival.LocationId && !approachNodeIds.Contains(node.Id)
            )
            .Select(node => new RerouteTarget(
                node.Id,
                null,
                graph.FindShortestPath(originNodeId, node.Id)
            ))
            .Where(candidate => candidate.Legs.Count > 0)
            .OrderBy(candidate => candidate.Legs.Sum(leg => leg.Distance))
            .FirstOrDefault();
        if (target == null)
        {
            return false;
        }
        var journey = await routing.Journeys.FindAsync([journeyId], cancellationToken);
        if (journey == null)
        {
            return false;
        }
        var index = await routing
            .JourneyLegs.Where(leg => leg.JourneyId == journeyId)
            .CountAsync(cancellationToken);
        routing.JourneyLegs.AddRange(
            target.Legs.Select(
                (leg, offset) =>
                    new JourneyLeg
                    {
                        JourneyId = journeyId,
                        Index = index + offset,
                        FromNodeId = leg.FromNodeId,
                        ToNodeId = leg.ToNodeId,
                        ConnectorId = leg.ConnectorId,
                        Distance = leg.Distance,
                        Path = leg.Path,
                        DwellAfter = TimeSpan.Zero,
                    }
            )
        );
        journey.DestinationPropId = target.PropId;
        journey.Status = JourneyStatus.Traveling;
        await routing.SaveChangesAsync(cancellationToken);
        return true;
    }

    private sealed record RerouteTarget(
        Guid NodeId,
        Guid? PropId,
        IReadOnlyList<DirectedTravelLeg> Legs
    );

    private static ExecuteCreatureJobCommand JobCommand(
        JourneyCompleted arrival,
        Creature creature
    ) =>
        new()
        {
            CreatureId = creature.Id,
            CurrentLocationId = creature.LocationId,
            CurrentCondition = creature.Condition,
            CurrentPosture = creature.Posture,
            CreatureJobAction = arrival.Action,
            JobLocationId = arrival.LocationId,
        };
}
