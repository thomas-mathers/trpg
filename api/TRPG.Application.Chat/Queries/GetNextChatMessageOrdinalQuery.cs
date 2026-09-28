using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.Chat.Queries;

public class GetNextChatMessageOrdinalQuery
{
    public required Guid SessionId { get; init; }
}

internal class GetNextChatMessageOrdinalQueryHandler(IChatDbContext context)
    : IQueryHandler<GetNextChatMessageOrdinalQuery, int>
{
    public async Task<int> Handle(
        GetNextChatMessageOrdinalQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var maxOrdinal =
            await context
                .ChatMessages.Where(m => m.SessionId == query.SessionId)
                .MaxAsync(m => (int?)m.Ordinal, cancellationToken)
            ?? -1;

        return maxOrdinal + 1;
    }
}
