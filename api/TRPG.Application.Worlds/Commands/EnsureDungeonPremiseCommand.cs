using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Commands;

public class EnsureDungeonPremiseCommand
{
    public required Guid RoomLocationId { get; init; }
}

// Written the first time someone walks in rather than at world generation: a world holds dozens of
// dungeons and most are never entered, so writing them all up front is paying for prose nobody
// reads.
internal class EnsureDungeonPremiseCommandHandler(
    IWorldsDbContext context,
    DungeonPremiseGenerator generator
) : ICommandHandler<EnsureDungeonPremiseCommand>
{
    public async Task Handle(
        EnsureDungeonPremiseCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var room = await context
            .Rooms.AsNoTracking()
            .FirstOrDefaultAsync(r => r.LocationId == command.RoomLocationId, cancellationToken);
        if (room == null)
        {
            return;
        }

        var building = await context.Buildings.FirstOrDefaultAsync(
            b => b.Id == room.BuildingId,
            cancellationToken
        );
        if (
            building == null
            || building.Premise != null
            || !BuildingTypes.Dungeon.Contains(building.BuildingType)
        )
        {
            return;
        }

        var roomNames = await context
            .Rooms.AsNoTracking()
            .Where(r => r.BuildingId == building.Id)
            .Select(r => r.Name)
            .ToArrayAsync(cancellationToken);

        var nearestSettlement = await context
            .Cities.AsNoTracking()
            .Where(city => city.WorldId == building.WorldId)
            .Select(city => city.Name)
            .FirstOrDefaultAsync(cancellationToken);

        building.Premise = await generator.Generate(
            new DungeonPremiseRequest(
                building.Name,
                building.BuildingType,
                nearestSettlement,
                roomNames
            ),
            cancellationToken
        );
        await context.SaveChangesAsync(cancellationToken);
    }
}
