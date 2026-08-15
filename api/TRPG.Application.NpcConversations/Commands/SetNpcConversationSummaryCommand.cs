using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.NpcConversations.Commands;

public class SetNpcConversationSummaryCommand
{
    public required Guid WorldId { get; init; }
    public required Guid CreatureId { get; init; }
    public required Guid NpcId { get; init; }
    public required string Summary { get; init; }
}

public class SetNpcConversationSummaryCommandHandler(TrpgDbContext context)
{
    public async Task Handle(
        SetNpcConversationSummaryCommand command,
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
