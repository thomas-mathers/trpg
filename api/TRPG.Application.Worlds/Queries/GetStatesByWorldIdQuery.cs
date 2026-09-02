using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Worlds.Queries;

public class GetStatesByWorldIdQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetStatesByWorldIdQueryHandler(IWorldsDbContext context)
    : IQueryHandler<GetStatesByWorldIdQuery, IReadOnlyCollection<State>>
{
    public async Task<IReadOnlyCollection<State>> Handle(
        GetStatesByWorldIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .States.AsNoTracking()
            .Where(state => state.WorldId == query.WorldId)
            .ToArrayAsync(cancellationToken);
}
