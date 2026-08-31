using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.NpcConversations.Commands;

public enum OpenNpcConversationResult
{
    Opened,
    AlreadyOpen,
}

public class OpenNpcConversationCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid NpcId { get; init; }
    public required string NpcName { get; init; }
}

internal class OpenNpcConversationCommandHandler(
    TrpgDbContext context,
    IDomainEventPublisher<NpcConversationStartedEvent> domainEvents
) : ICommandHandler<OpenNpcConversationCommand, OpenNpcConversationResult>
{
    public async Task<OpenNpcConversationResult> Handle(
        OpenNpcConversationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );

        var state = await context.NpcConversationSessionStates.FirstOrDefaultAsync(
            s => s.SessionId == command.SessionId,
            cancellationToken
        );
        if (state == null)
        {
            state = new NpcConversationSessionState
            {
                SessionId = command.SessionId,
                WorldId = command.WorldId,
            };
            context.NpcConversationSessionStates.Add(state);
        }
        else if (state.OpenConversationCreatureIdsByName.ContainsKey(command.NpcName))
        {
            await transaction.CommitAsync(cancellationToken);
            return OpenNpcConversationResult.AlreadyOpen;
        }

        state.OpenConversationCreatureIdsByName[command.NpcName] = command.NpcId;
        await context.SaveChangesAsync(cancellationToken);

        await domainEvents.Publish(
            new NpcConversationStartedEvent(
                PlayerId: command.PlayerId,
                WorldId: command.WorldId,
                NpcId: command.NpcId
            ),
            cancellationToken
        );
        await transaction.CommitAsync(cancellationToken);
        return OpenNpcConversationResult.Opened;
    }
}
