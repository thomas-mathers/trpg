using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Knowledge.Queries;

public class GetFactByIdQuery
{
    public required Guid FactId { get; init; }
}

internal class GetFactByIdQueryHandler(IKnowledgeDbContext context)
    : IQueryHandler<GetFactByIdQuery, Fact?>
{
    public async Task<Fact?> Handle(
        GetFactByIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Facts.AsNoTracking()
            .FirstOrDefaultAsync(fact => fact.Id == query.FactId, cancellationToken);
}
