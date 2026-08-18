using System.Transactions;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.NpcConversations.Commands;

public enum CloseNpcConversationResult
{
    Closed,
    NotOpen,
}

public class CloseNpcConversationCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required string NpcName { get; init; }
    public required string ConversationSummary { get; init; }
    public required string Summary { get; init; }
}

internal class CloseNpcConversationCommandHandler(
    IQueryHandler<GetGameSessionQuery, GameSession> getGameSession,
    ICommandHandler<RecordNpcConversationCommand> recordNpcConversation,
    ICommandHandler<UpdateGameSessionCommand> updateGameSession
) : ICommandHandler<CloseNpcConversationCommand, CloseNpcConversationResult>
{
    public async Task<CloseNpcConversationResult> Handle(
        CloseNpcConversationCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var snapshot = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = command.SessionId },
            cancellationToken
        );
        if (!snapshot.OpenConversationCreatureIdsByName.TryGetValue(command.NpcName, out var npcId))
        {
            return CloseNpcConversationResult.NotOpen;
        }

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        await recordNpcConversation.Handle(
            new RecordNpcConversationCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                NpcId = npcId,
                ConversationSummary = command.ConversationSummary,
                Summary = command.Summary,
            },
            cancellationToken
        );

        snapshot.OpenConversationCreatureIdsByName.Remove(command.NpcName);

        await updateGameSession.Handle(
            new UpdateGameSessionCommand
            {
                SessionId = command.SessionId,
                OpenConversationCreatureIdsByName = snapshot.OpenConversationCreatureIdsByName,
            },
            cancellationToken
        );

        transaction.Complete();

        return CloseNpcConversationResult.Closed;
    }
}
