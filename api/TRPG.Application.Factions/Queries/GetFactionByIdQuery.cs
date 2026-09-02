using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Factions.Queries;

public class GetFactionByIdQuery
{
    public required Guid Id { get; init; }
}

internal class GetFactionByIdQueryHandler(IFactionsDbContext context)
    : IQueryHandler<GetFactionByIdQuery, Faction?>
{
    public async Task<Faction?> Handle(
        GetFactionByIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Factions.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == query.Id, cancellationToken);
}
