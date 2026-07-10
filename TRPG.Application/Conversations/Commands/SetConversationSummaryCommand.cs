using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Conversations.Commands;

internal class SetConversationSummaryCommand
{
    public required Guid WorldId { get; init; }
    public required Guid CreatureId { get; init; }
    public required Guid NpcId { get; init; }
    public required string Summary { get; init; }
}

internal class SetConversationSummaryCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        SetConversationSummaryCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var conversation = await context.NpcConversations.FirstOrDefaultAsync(
            c => c.CreatureId == command.CreatureId && c.NpcId == command.NpcId,
            cancellationToken
        );

        if (conversation == null)
        {
            context.NpcConversations.Add(
                new NpcConversation
                {
                    WorldId = command.WorldId,
                    CreatureId = command.CreatureId,
                    NpcId = command.NpcId,
                    Summary = command.Summary,
                }
            );
        }
        else
        {
            conversation.Summary = command.Summary;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
