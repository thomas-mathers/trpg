using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Worlds.Commands;

public record EnsureExpeditionJournalContextCommand(Guid WorkId);

public record ExpeditionJournalContext(
    string Author,
    string DungeonHistory,
    string Purpose,
    string Separation,
    string FinalExperience,
    IReadOnlyList<string> Route
);

internal class EnsureExpeditionJournalContextCommandHandler(
    IWorldsDbContext context,
    ICommandHandler<EnsureDungeonPremiseCommand> ensurePremise
) : ICommandHandler<EnsureExpeditionJournalContextCommand, ExpeditionJournalContext?>
{
    public async Task<ExpeditionJournalContext?> Handle(
        EnsureExpeditionJournalContextCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var expedition = await context
            .DungeonExpeditions.AsNoTracking()
            .FirstOrDefaultAsync(
                expedition => expedition.JournalWorkId == command.WorkId,
                cancellationToken
            );
        if (expedition == null)
            return null;

        await ensurePremise.Handle(
            new EnsureDungeonPremiseCommand { BuildingId = expedition.BuildingId },
            cancellationToken
        );

        var premise = await context
            .Buildings.AsNoTracking()
            .Where(building => building.Id == expedition.BuildingId)
            .Select(building => building.Premise)
            .SingleAsync(cancellationToken);

        var routeIds = expedition
            .KnownRouteLocationIds.Append(expedition.CompanionLocationId)
            .ToArray();

        var names = await context
            .Locations.AsNoTracking()
            .Where(location => routeIds.AsEnumerable().Contains(location.Id))
            .ToDictionaryAsync(
                location => location.Id,
                location => location.Name,
                cancellationToken
            );

        var route = routeIds.Select(id => names.GetValueOrDefault(id, "unknown room")).ToArray();
        return new ExpeditionJournalContext(
            Author: expedition.CompanionName,
            DungeonHistory: premise ?? "",
            Purpose: expedition.Purpose,
            Separation: expedition.Separation,
            FinalExperience: expedition.FinalExperience,
            Route: route
        );
    }
}
