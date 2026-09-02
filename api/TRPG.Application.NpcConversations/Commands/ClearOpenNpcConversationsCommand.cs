using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.NpcConversations.Commands;

public class ClearOpenNpcConversationsCommand
{
    public required Guid SessionId { get; init; }
}

internal class ClearOpenNpcConversationsCommandHandler(INpcConversationsDbContext context)
    : ICommandHandler<ClearOpenNpcConversationsCommand>
{
    public async Task Handle(
        ClearOpenNpcConversationsCommand command,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .NpcConversationSessionStates.Where(s => s.SessionId == command.SessionId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(s => s.OpenConversationCreatureIdsByName, []),
                cancellationToken
            );
}
