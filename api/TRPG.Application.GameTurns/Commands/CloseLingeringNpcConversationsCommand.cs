using System.Transactions;
using Microsoft.Extensions.Logging;
using TRPG.Application.Chat.Commands;
using TRPG.Application.Chat.Queries;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.NpcConversations.Commands;
using TRPG.Application.NpcConversations.Queries;
using TRPG.Domain;

namespace TRPG.Application.GameTurns.Commands;

public class CloseLingeringNpcConversationsCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required GameInstant GameTime { get; init; }
    public required int CurrentTurnStart { get; init; }
}

internal class CloseLingeringNpcConversationsCommandHandler(
    LlmConversationClient llmConversationClient,
    IQueryHandler<GetOpenNpcConversationsQuery, Dictionary<string, Guid>> getOpenNpcConversations,
    IQueryHandler<GetNextChatMessageOrdinalQuery, int> getNextChatMessageOrdinal,
    ICommandHandler<ClearOpenNpcConversationsCommand> clearOpenNpcConversations,
    ICommandHandler<ReleaseCreaturesCommand> releaseCreatures,
    ICommandHandler<ClearChatMessagesCommand> clearChatMessages,
    ICommandHandler<RemoveChatMessagesFromOrdinalCommand> removeChatMessagesFromOrdinal,
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

        // Captured before the forced end_conversation exchanges below so that bookkeeping can be purged afterward.
        var forceEndBoundary =
            openConversations.Count == 0
                ? (int?)null
                : await getNextChatMessageOrdinal.Handle(
                    new GetNextChatMessageOrdinalQuery { SessionId = command.SessionId },
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

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        await ReleaseUnendedConversations(command, stillOpenConversations, cancellationToken);

        await clearOpenNpcConversations.Handle(
            new ClearOpenNpcConversationsCommand { SessionId = command.SessionId },
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

        if (forceEndBoundary != null)
        {
            await removeChatMessagesFromOrdinal.Handle(
                new RemoveChatMessagesFromOrdinalCommand
                {
                    SessionId = command.SessionId,
                    FromOrdinal = forceEndBoundary.Value,
                },
                cancellationToken
            );
        }

        transaction.Complete();
    }

    private async Task ReleaseUnendedConversations(
        CloseLingeringNpcConversationsCommand command,
        Dictionary<string, Guid> stillOpenConversations,
        CancellationToken cancellationToken
    )
    {
        if (stillOpenConversations.Count == 0)
        {
            return;
        }

        await releaseCreatures.Handle(
            new ReleaseCreaturesCommand
            {
                WorldId = command.WorldId,
                CreatureIds = [command.PlayerId, .. stillOpenConversations.Values],
                GameTime = command.GameTime,
            },
            cancellationToken
        );
    }

    private async Task ForceEndConversation(string npcName, CancellationToken cancellationToken)
    {
        var prompt =
            $"Before continuing, call end_conversation for {npcName} to save a summary of your conversation.";
        var reply = await llmConversationClient.StreamReply(prompt, cancellationToken);

        await foreach (var _ in reply.Tokens.WithCancellation(cancellationToken)) { }
    }
}
