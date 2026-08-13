using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TRPG.Application.Chat;
using TRPG.Application.Chat.Commands;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.NpcConversations.Queries;
using TRPG.Data;

namespace TRPG.Application.NpcConversations.Commands;

internal class CloseLingeringNpcConversationsCommand
{
    public required Guid SessionId { get; init; }
    public required int CurrentTurnStart { get; init; }
}

internal class CloseLingeringNpcConversationsCommandHandler(
    LlmConversationClient llmConversationClient,
    TrpgDbContext context,
    GetOpenNpcConversationsQueryHandler getOpenNpcConversations,
    UpdateGameSessionCommandHandler updateGameSession,
    ClearChatMessagesCommandHandler clearChatMessages,
    ILogger<CloseLingeringNpcConversationsCommandHandler> logger
)
{
    public async Task Handle(
        CloseLingeringNpcConversationsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();

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

        if (openConversations.Count > 0)
        {
            logger.LogInformation(
                "[perf] CloseLingeringNpcConversationsCommand closed {Count} conversation(s) in {ElapsedMs}ms",
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
        var reply = await llmConversationClient.StreamReply(
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
