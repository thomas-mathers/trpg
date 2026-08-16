using Microsoft.Extensions.Logging;
using TRPG.Application.Chat.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameTurns;
using TRPG.Application.NpcConversations.Queries;
using TRPG.Data;

namespace TRPG.Application.GameTurns.Commands;

public class CloseLingeringNpcConversationsCommand
{
    public required Guid SessionId { get; init; }
    public required int CurrentTurnStart { get; init; }
}

internal class CloseLingeringNpcConversationsCommandHandler(
    LlmConversationClient llmConversationClient,
    TrpgDbContext context,
    IQueryHandler<GetOpenNpcConversationsQuery, Dictionary<string, Guid>> getOpenNpcConversations,
    ICommandHandler<UpdateGameSessionCommand> updateGameSession,
    ICommandHandler<ClearChatMessagesCommand> clearChatMessages,
    ILogger<CloseLingeringNpcConversationsCommandHandler> logger
) : ICommandHandler<CloseLingeringNpcConversationsCommand>
{
    public async Task Handle(
        CloseLingeringNpcConversationsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var openConversations = await getOpenNpcConversations.Handle(
            new GetOpenNpcConversationsQuery { SessionId = command.SessionId },
            cancellationToken
        );

        foreach (var npcName in openConversations.Keys)
        {
            await ForceEndConversation(npcName, cancellationToken);
        }

        var stillOpenConversations = await getOpenNpcConversations.Handle(
            new GetOpenNpcConversationsQuery { SessionId = command.SessionId },
            cancellationToken
        );

        foreach (var npcName in openConversations.Keys.Intersect(stillOpenConversations.Keys))
        {
            logger.LogWarning("[game] Failed to save conversation summary for {NpcName}", npcName);
        }

        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );

        await updateGameSession.Handle(
            new UpdateGameSessionCommand
            {
                SessionId = command.SessionId,
                OpenConversationCreatureIdsByName = [],
            },
            cancellationToken
        );

        await clearChatMessages.Handle(
            new ClearChatMessagesCommand
            {
                SessionId = command.SessionId,
                KeepFromOrdinal = command.CurrentTurnStart,
            },
            cancellationToken
        );

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task ForceEndConversation(string npcName, CancellationToken cancellationToken)
    {
        var prompt =
            $"Before continuing, call end_conversation for {npcName} to save a summary of your conversation.";
        var reply = await llmConversationClient.StreamReply(
            prompt,
            includeTools: true,
            cancellationToken
        );

        await foreach (var _ in reply.Tokens.WithCancellation(cancellationToken)) { }
    }
}
