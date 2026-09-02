using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetCountriesByWorldIdQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetCountriesByWorldIdQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetCountriesByWorldIdQuery, IReadOnlyCollection<Country>>
{
    public async Task<IReadOnlyCollection<Country>> Handle(
        GetCountriesByWorldIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Countries.AsNoTracking()
            .Where(country => country.WorldId == query.WorldId)
            .ToArrayAsync(cancellationToken);
}
