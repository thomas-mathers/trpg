using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetAllCreaturesInWorldQuery
{
    public required Guid WorldId { get; init; }
}

public record CreatureSummary(Guid Id, string Name, CreatureType CreatureType, string Biography);

internal class GetAllCreaturesInWorldQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetAllCreaturesInWorldQuery, IReadOnlyCollection<CreatureSummary>>
{
    public async Task<IReadOnlyCollection<CreatureSummary>> Handle(
        GetAllCreaturesInWorldQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Creatures.AsNoTracking()
            .Where(creature => creature.WorldId == query.WorldId)
            .Select(creature => new CreatureSummary(
                creature.Id,
                creature.Name,
                creature.CreatureType,
                creature.Biography
            ))
            .ToArrayAsync(cancellationToken);
}
