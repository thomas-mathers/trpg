using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Data;

namespace TRPG.Application.Creatures.Queries;

public class GetCreatureLevelQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetCreatureLevelQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetCreatureLevelQuery, int>
{
    public async Task<int> Handle(
        GetCreatureLevelQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Creatures.AsNoTracking()
            .Where(c => c.Id == query.CreatureId)
            .Select(c => c.Level)
            .FirstAsync(cancellationToken);
    }
}
