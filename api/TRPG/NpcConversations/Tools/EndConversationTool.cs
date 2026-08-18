using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.GameTurns;
using TRPG.Application.NpcConversations.Commands;
using TRPG.Tools;

namespace TRPG.NpcConversations.Tools;

internal class EndConversationTool(
    GameTurnContext turnContext,
    ICommandHandler<CloseNpcConversationCommand, CloseNpcConversationResult> closeNpcConversation,
    ILogger<EndConversationTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("end_conversation")]
    [Description(
        "Call this when a conversation with someone winds down or the topic changes significantly, to save both this conversation and an updated long-term summary for future meetings."
    )]
    private async Task<object?> InvokeAsync(
        [Description(
            "The exact Name of the person you spoke with, copied verbatim from the most recent look or move result."
        )]
            string npcName,
        [Description("A concise, third-person, factual summary of this conversation only.")]
            string conversationSummary,
        [Description(
            "A concise, third-person, factual summary of the NPC's longer history with the player, updated to include this conversation."
        )]
            string summary,
        [Description(
            "New durable facts the player stated about themselves that should be remembered indefinitely — name, origin, allegiance, and similar. Do not include anything about the NPC, or anything already covered by quest or reputation state. Omit if nothing new was learned."
        )]
            IReadOnlyCollection<string>? durableFactsAdded = null,
        [Description(
            "The numbers of durable facts, from this conversation's start_conversation DurableFacts list, that the player has now contradicted and should be retracted. Omit if none were contradicted."
        )]
            IReadOnlyCollection<int>? durableFactsRemoved = null,
        [Description(
            "New unresolved threads with the player — a promise, an unanswered question, something to circle back to. Only for things with a natural resolution, not permanent facts. Omit if nothing new is pending."
        )]
            IReadOnlyCollection<string>? openThreadsAdded = null,
        [Description(
            "The numbers of open threads, from this conversation's start_conversation OpenThreads list, that were resolved or addressed this conversation. Omit if none were resolved."
        )]
            IReadOnlyCollection<int>? openThreadsRemoved = null,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogInformation("[end_conversation] npcName={NpcName}", npcName);
        var stopwatch = Stopwatch.StartNew();

        var outcome = await closeNpcConversation.Handle(
            new CloseNpcConversationCommand
            {
                SessionId = turnContext.SessionId,
                WorldId = turnContext.WorldId,
                PlayerId = turnContext.PlayerId,
                NpcName = npcName,
                ConversationSummary = conversationSummary,
                Summary = summary,
                DurableFactsAdded = durableFactsAdded ?? [],
                DurableFactsRemoved = durableFactsRemoved ?? [],
                OpenThreadsAdded = openThreadsAdded ?? [],
                OpenThreadsRemoved = openThreadsRemoved ?? [],
            },
            cancellationToken
        );
        if (outcome == CloseNpcConversationResult.NotOpen)
        {
            return new ToolError(
                $"No open conversation with '{npcName}'. Call start_conversation first."
            );
        }

        var result = new { Saved = true };
        logger.LogInformation(
            "[perf] [end_conversation] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(
                result,
                Application.Common.Serialization.TrpgJsonOptions.Default
            )
        );
        return result;
    }
}
