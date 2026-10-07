using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Props.Queries;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class PlaceCreaturesAtLocationCommand
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
    public required Guid LocationId { get; init; }
    public GameInstant? ArrivedAt { get; init; }
}

internal class PlaceCreaturesAtLocationCommandHandler(
    ICreaturesDbContext context,
    IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>> getLocationsByIds,
    IQueryHandler<GetPropsByLocationIdQuery, IReadOnlyCollection<Prop>> getPropsByLocation,
    IQueryHandler<
        GetBuildingsByLocationQuery,
        IReadOnlyCollection<Building>
    > getBuildingsByLocation,
    IQueryHandler<
        GetConnectorsByOriginLocationIdsQuery,
        IReadOnlyCollection<LocationConnector>
    > getConnectorsByOrigins,
    IQueryHandler<GetWorldPlayerIdQuery, Guid?> getWorldPlayerId
) : ICommandHandler<PlaceCreaturesAtLocationCommand>
{
    public async Task Handle(
        PlaceCreaturesAtLocationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.CreatureIds.Count == 0)
        {
            return;
        }

        var locations = await getLocationsByIds.Handle(
            new GetLocationsByIdsQuery { Ids = [command.LocationId] },
            cancellationToken
        );
        if (
            !locations.TryGetValue(command.LocationId, out var location)
            || location.Width <= 0
            || location.Depth <= 0
        )
        {
            return;
        }

        var arriving = await context
            .Creatures.AsNoTracking()
            .Where(creature => command.CreatureIds.AsEnumerable().Contains(creature.Id))
            .ToListAsync(cancellationToken);

        var resident = await context
            .Creatures.AsNoTracking()
            .Where(creature => creature.LocationId == command.LocationId)
            .Where(creature => !command.CreatureIds.AsEnumerable().Contains(creature.Id))
            .ToListAsync(cancellationToken);

        var input = await BuildInput(location, arriving, resident, cancellationToken);
        var playerId = await getWorldPlayerId.Handle(
            new GetWorldPlayerIdQuery { WorldId = location.WorldId },
            cancellationToken
        );

        var entries =
            command.ArrivedAt != null && resident.Any(creature => creature.Id == playerId)
                ? PlaceEntryPoints(input, playerId)
                : new Dictionary<Guid, Point>();

        PlaceArrivals(input, playerId);

        await WritePoses(arriving, entries, command.ArrivedAt, cancellationToken);
    }

    private static Dictionary<Guid, Point> PlaceEntryPoints(
        CreatureLayoutInput input,
        Guid? playerId
    )
    {
        var walkers = input.Creatures.Where(creature => creature.Id != playerId).ToArray();

        CreatureLayoutGenerator.PlaceAtArrival(input with { Creatures = walkers });

        return walkers.ToDictionary(
            creature => creature.Id,
            creature => new Point(creature.X, creature.Y)
        );
    }

    private async Task<CreatureLayoutInput> BuildInput(
        Location location,
        IReadOnlyList<Creature> arriving,
        IReadOnlyList<Creature> resident,
        CancellationToken cancellationToken
    )
    {
        var props = await getPropsByLocation.Handle(
            new GetPropsByLocationIdQuery { LocationId = location.Id },
            cancellationToken
        );
        var buildings = await getBuildingsByLocation.Handle(
            new GetBuildingsByLocationQuery { LocationId = location.Id },
            cancellationToken
        );
        var originIds = arriving
            .Select(creature => creature.PreviousLocationId)
            .OfType<Guid>()
            .Append(location.Id)
            .Distinct()
            .ToArray();
        var connectors = await getConnectorsByOrigins.Handle(
            new GetConnectorsByOriginLocationIdsQuery { OriginLocationIds = originIds },
            cancellationToken
        );

        return new CreatureLayoutInput(
            [location],
            [.. props],
            [.. buildings],
            [.. connectors],
            arriving
        )
        {
            AlreadyPlaced = resident,
        };
    }

    private static void PlaceArrivals(CreatureLayoutInput input, Guid? playerId)
    {
        var player = input.Creatures.Where(creature => creature.Id == playerId).ToArray();
        var others = input.Creatures.Where(creature => creature.Id != playerId).ToArray();

        CreatureLayoutGenerator.PlaceAtArrival(input with { Creatures = player });
        CreatureLayoutGenerator.Place(
            input with
            {
                Creatures = others,
                AlreadyPlaced = [.. input.AlreadyPlaced, .. player],
            }
        );
    }

    private async Task WritePoses(
        IReadOnlyList<Creature> creatures,
        IReadOnlyDictionary<Guid, Point> entries,
        GameInstant? arrivedAt,
        CancellationToken cancellationToken
    )
    {
        foreach (var creature in creatures)
        {
            var x = creature.X;
            var y = creature.Y;
            var angle = creature.Angle;
            var entry = entries.GetValueOrDefault(creature.Id);
            var entryX = entry?.X;
            var entryY = entry?.Y;
            var enteredAt = entry == null ? null : arrivedAt;

            await context
                .Creatures.Where(candidate => candidate.Id == creature.Id)
                .ExecuteUpdateAsync(
                    setters =>
                        setters
                            .SetProperty(candidate => candidate.X, x)
                            .SetProperty(candidate => candidate.Y, y)
                            .SetProperty(candidate => candidate.Angle, angle)
                            .SetProperty(candidate => candidate.EntryX, entryX)
                            .SetProperty(candidate => candidate.EntryY, entryY)
                            .SetProperty(candidate => candidate.EnteredAt, enteredAt),
                    cancellationToken
                );
        }
    }
}
