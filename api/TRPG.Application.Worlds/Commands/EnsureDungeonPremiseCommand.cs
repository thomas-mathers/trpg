using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Commands;

public class EnsureDungeonPremiseCommand
{
    public required Guid BuildingId { get; init; }
}

// Written the first time someone walks in, or the first time a nearby scene warms it up ahead of
// that, rather than at world generation: a world holds dozens of dungeons and most are never
// entered, so writing them all up front is paying for prose nobody reads.
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
        var building = await context
            .Buildings.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == command.BuildingId, cancellationToken);
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

        var expedition = await context
            .DungeonExpeditions.AsNoTracking()
            .FirstOrDefaultAsync(
                expedition => expedition.BuildingId == building.Id,
                cancellationToken
            );

        var premise = await generator.Generate(
            new DungeonPremiseRequest(
                building.Name,
                building.BuildingType,
                nearestSettlement,
                roomNames,
                expedition == null
                    ? null
                    : $"{expedition.Purpose} {expedition.Separation} {expedition.FinalExperience}"
            ),
            cancellationToken
        );

        // Written unconditionally rather than checked first, because a prefetch triggered while
        // still outside can overtake the write triggered by actually walking in. Whichever lands
        // first is the history; the other is discarded.
        await context
            .Buildings.Where(b => b.Id == building.Id && b.Premise == null)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Premise, premise), cancellationToken);
    }
}
