using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Application.Chat;
using TRPG.Application.Chat.Commands;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Data;

namespace TRPG.Application.GameTurns;

internal class LingeringConversationCloser(
    GameChatCompletionStreamer chatCompletionStreamer,
    TrpgDbContext context,
    GameTurnContext turnContext,
    GetOpenConversationsQueryHandler getOpenConversations,
    UpdateGameSessionCommandHandler updateGameSession,
    ClearChatMessagesCommandHandler clearChatMessages,
    ILogger<LingeringConversationCloser> logger
)
{
    public async Task CloseAll(int currentTurnStart, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var openConversations = await getOpenConversations.Handle(
            new GetOpenConversationsQuery { SessionId = turnContext.SessionId },
            cancellationToken
        );

        foreach (var npcName in openConversations.Keys)
        {
            await ForceEndConversation(npcName, cancellationToken);
        }

        var stillOpenConversations = await getOpenConversations.Handle(
            new GetOpenConversationsQuery { SessionId = turnContext.SessionId },
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
                SessionId = turnContext.SessionId,
                OpenConversationCreatureIdsByName = [],
            },
            cancellationToken
        );

        await clearChatMessages.Handle(
            new ClearChatMessagesCommand
            {
                SessionId = turnContext.SessionId,
                KeepFromOrdinal = currentTurnStart,
            },
            cancellationToken
        );

        await transaction.CommitAsync(cancellationToken);

        if (openConversations.Count > 0)
        {
            logger.LogInformation(
                "[perf] CloseAll closed {Count} conversation(s) in {ElapsedMs}ms",
                openConversations.Count,
                stopwatch.ElapsedMilliseconds
            );
        }
    }

    private async Task ForceEndConversation(string npcName, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var prompt =
            $"Before continuing, call end_conversation for {npcName} to save a summary of your conversation.";
        var reply = await chatCompletionStreamer.StreamReply(
            prompt,
            includeTools: true,
            cancellationToken
        );

        await foreach (var _ in reply.Tokens.WithCancellation(cancellationToken)) { }

        logger.LogInformation(
            "[perf] ForceEndConversation for {NpcName} took {ElapsedMs}ms",
            npcName,
            stopwatch.ElapsedMilliseconds
        );
    }
}
