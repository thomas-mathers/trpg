using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public record WalkExit(Guid ConnectorId, GameInstant DepartedAt, GameInstant? ArrivedAt)
{
    public bool IsPassThrough => ArrivedAt == DepartedAt;

    public static WalkExit From(RouteTimelinePosition.InTransit inTransit) =>
        new(inTransit.ConnectorId, inTransit.DepartedAtGameTime, inTransit.ArrivedAtGameTime);
}

public class StartWalkingCommand
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
    public WalkExit? Exit { get; init; }
}

internal class StartWalkingCommandHandler(
    ICreaturesDbContext context,
    IQueryHandler<
        GetConnectorsByOriginLocationIdsQuery,
        IReadOnlyCollection<PlacedConnector>
    > getConnectorsByOrigins,
    IDomainEventPublisher<CreaturesStartedWalkingEvent> domainEvents
) : ICommandHandler<StartWalkingCommand>
{
    public async Task Handle(
        StartWalkingCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var living = await context
            .Creatures.Where(creature =>
                command.CreatureIds.Contains(creature.Id)
                && creature.Condition != CreatureCondition.Dead
            )
            .Select(creature => new LivingCreature(
                creature.Id,
                creature.LocationId,
                creature.PreviousLocationId
            ))
            .ToListAsync(cancellationToken);
        if (living.Count == 0)
        {
            return;
        }

        var livingIds = living.Select(creature => creature.Id).ToList();
        await context
            .Creatures.Where(creature => livingIds.Contains(creature.Id))
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(creature => creature.Condition, CreatureCondition.Awake)
                        .SetProperty(creature => creature.Activity, (CreatureActivity?)null)
                        .SetProperty(creature => creature.Posture, CreaturePosture.Standing)
                        .SetProperty(creature => creature.Movement, CreatureMovement.Walking),
                cancellationToken
            );
        await RecordWalks(living, command.Exit, cancellationToken);
        await domainEvents.Publish(new CreaturesStartedWalkingEvent(livingIds), cancellationToken);
    }

    private async Task RecordWalks(
        IReadOnlyList<LivingCreature> creatures,
        WalkExit? exit,
        CancellationToken cancellationToken
    )
    {
        var connectors = await GetConnectors(creatures, exit, cancellationToken);

        foreach (
            var group in creatures.GroupBy(creature =>
                (creature.LocationId, creature.PreviousLocationId)
            )
        )
        {
            var walk = PlanWalk(
                group.Key.LocationId,
                group.Key.PreviousLocationId,
                exit,
                connectors
            );
            var ids = group.Select(creature => creature.Id).ToList();
            var entryX = walk.Entry?.X;
            var entryY = walk.Entry?.Y;
            var exitX = walk.ExitPoint?.X;
            var exitY = walk.ExitPoint?.Y;

            await context
                .Creatures.Where(creature => ids.Contains(creature.Id))
                .ExecuteUpdateAsync(
                    setters =>
                        setters
                            .SetProperty(creature => creature.EntryX, entryX)
                            .SetProperty(creature => creature.EntryY, entryY)
                            .SetProperty(creature => creature.EnteredAt, walk.EnteredAt)
                            .SetProperty(creature => creature.ExitX, exitX)
                            .SetProperty(creature => creature.ExitY, exitY)
                            .SetProperty(creature => creature.DepartedAt, walk.DepartedAt),
                    cancellationToken
                );
        }
    }

    private async Task<IReadOnlyCollection<PlacedConnector>> GetConnectors(
        IReadOnlyList<LivingCreature> creatures,
        WalkExit? exit,
        CancellationToken cancellationToken
    )
    {
        if (exit == null)
        {
            return [];
        }

        var originIds = creatures
            .Select(creature => creature.LocationId)
            .Concat(creatures.Select(creature => creature.PreviousLocationId).OfType<Guid>())
            .Distinct()
            .ToArray();

        return await getConnectorsByOrigins.Handle(
            new GetConnectorsByOriginLocationIdsQuery { OriginLocationIds = originIds },
            cancellationToken
        );
    }

    private static WalkRecord PlanWalk(
        Guid locationId,
        Guid? previousLocationId,
        WalkExit? exit,
        IReadOnlyCollection<PlacedConnector> connectors
    )
    {
        var exitConnector = connectors.FirstOrDefault(placed =>
            placed.Connector.Id == exit?.ConnectorId
            && placed.Connector.OriginLocationId == locationId
        );
        if (exit == null || exitConnector == null)
        {
            return WalkRecord.None;
        }

        var entryConnector = exit.IsPassThrough
            ? connectors.FirstOrDefault(placed =>
                placed.Connector.OriginLocationId == previousLocationId
                && placed.Connector.DestinationLocationId == locationId
            )
            : null;

        return new WalkRecord(
            entryConnector == null ? null : entryConnector.Arrival,
            entryConnector == null ? null : exit.DepartedAt,
            exitConnector.Exit,
            exit.DepartedAt
        );
    }

    private record LivingCreature(Guid Id, Guid LocationId, Guid? PreviousLocationId);

    private record WalkRecord(
        Point? Entry,
        GameInstant? EnteredAt,
        Point? ExitPoint,
        GameInstant? DepartedAt
    )
    {
        public static readonly WalkRecord None = new(null, null, null, null);
    }
}
