using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.NpcConversations.Commands;

public class RecordNpcConversationCommand
{
    public required string ConversationSummary { get; init; }
    public required Guid NpcId { get; init; }
    public required Guid PlayerId { get; init; }
    public required string Summary { get; init; }
    public required Guid WorldId { get; init; }
}

internal class RecordNpcConversationCommandHandler(TrpgDbContext context)
    : ICommandHandler<RecordNpcConversationCommand>
{
    public async Task Handle(
        RecordNpcConversationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var history = await context.NpcConversationHistories.FirstOrDefaultAsync(
            item => item.CreatureId == command.PlayerId && item.NpcId == command.NpcId,
            cancellationToken
        );

        if (history == null)
        {
            history = new NpcConversationHistory
            {
                WorldId = command.WorldId,
                CreatureId = command.PlayerId,
                NpcId = command.NpcId,
                Summary = command.Summary,
            };
            context.NpcConversationHistories.Add(history);
        }
        else
        {
            history.Summary = command.Summary;
        }

        context.NpcConversations.Add(
            new NpcConversation
            {
                WorldId = command.WorldId,
                NpcConversationHistoryId = history.Id,
                Summary = command.ConversationSummary,
            }
        );

        await context.SaveChangesAsync(cancellationToken);
    }
}
