using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetCreatureBaseAttributesQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetCreatureBaseAttributesQueryHandler(ICreaturesDbContext context)
    : IQueryHandler<GetCreatureBaseAttributesQuery, Attributes>
{
    public async Task<Attributes> Handle(
        GetCreatureBaseAttributesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Creatures.AsNoTracking()
            .Where(c => c.Id == query.CreatureId)
            .Select(c => c.BaseAttributes)
            .FirstAsync(cancellationToken);
    }
}
