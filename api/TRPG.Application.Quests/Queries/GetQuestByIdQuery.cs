using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Quests.Queries;

public class GetQuestByIdQuery
{
    public required Guid Id { get; init; }
}

internal class GetQuestByIdQueryHandler(IQuestsDbContext context)
    : IQueryHandler<GetQuestByIdQuery, Quest?>
{
    public async Task<Quest?> Handle(
        GetQuestByIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Quests.AsNoTracking()
            .FirstOrDefaultAsync(quest => quest.Id == query.Id, cancellationToken);
}
