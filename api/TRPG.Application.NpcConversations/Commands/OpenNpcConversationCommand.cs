using TRPG.Application.Common.Events;
using TRPG.Application.Common.Handling;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
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
    public required Guid NpcId { get; init; }
    public required string NpcName { get; init; }
}

internal class OpenNpcConversationCommandHandler(
    TrpgDbContext context,
    IDomainEventPublisher<NpcConversationStartedEvent> domainEvents,
    IQueryHandler<GetGameSessionQuery, GameSession> getGameSession,
    ICommandHandler<UpdateGameSessionCommand> updateGameSession
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
        var snapshot = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = command.SessionId },
            cancellationToken
        );
        if (snapshot.OpenConversationCreatureIdsByName.ContainsKey(command.NpcName))
        {
            await transaction.CommitAsync(cancellationToken);
            return OpenNpcConversationResult.AlreadyOpen;
        }

        snapshot.OpenConversationCreatureIdsByName[command.NpcName] = command.NpcId;
        await updateGameSession.Handle(
            new UpdateGameSessionCommand
            {
                SessionId = command.SessionId,
                OpenConversationCreatureIdsByName = snapshot.OpenConversationCreatureIdsByName,
            },
            cancellationToken
        );

        await domainEvents.Publish(
            new NpcConversationStartedEvent(
                PlayerId: snapshot.PlayerId,
                WorldId: snapshot.WorldId,
                NpcId: command.NpcId
            ),
            cancellationToken
        );
        await transaction.CommitAsync(cancellationToken);
        return OpenNpcConversationResult.Opened;
    }
}
