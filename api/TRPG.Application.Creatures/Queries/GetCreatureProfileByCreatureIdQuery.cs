using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetCreatureProfileByCreatureIdQuery
{
    public required Guid CreatureId { get; init; }
    public required Guid WorldId { get; init; }
}

internal class GetCreatureProfileByCreatureIdQueryHandler(ICreaturesDbContext context)
    : IQueryHandler<GetCreatureProfileByCreatureIdQuery, CreatureProfile?>
{
    public async Task<CreatureProfile?> Handle(
        GetCreatureProfileByCreatureIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .CreatureProfiles.AsNoTracking()
            .FirstOrDefaultAsync(
                profile =>
                    profile.CreatureId == query.CreatureId && profile.WorldId == query.WorldId,
                cancellationToken
            );
}
