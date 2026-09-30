using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;

namespace TRPG.Application.NpcConversations.Commands;

public class ClearWorldOpenNpcConversationsCommand
{
    public required Guid WorldId { get; init; }
}

internal class ClearWorldOpenNpcConversationsCommandHandler(INpcConversationsDbContext context)
    : ICommandHandler<ClearWorldOpenNpcConversationsCommand>
{
    public async Task Handle(
        ClearWorldOpenNpcConversationsCommand command,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .NpcConversationSessionStates.Where(s => s.WorldId == command.WorldId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(s => s.OpenConversationCreatureIdsByName, []),
                cancellationToken
            );
}
