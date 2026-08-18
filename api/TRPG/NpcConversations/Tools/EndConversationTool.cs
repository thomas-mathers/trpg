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
        "Call this when a conversation with someone winds down or the topic changes significantly, to save this conversation, an updated long-term summary, and any durable facts or open threads for future meetings. summary is a narrative recap and durableFactsAdded/openThreadsAdded are a separate, mandatory checklist — writing a good summary does NOT excuse skipping them. Before every call, ask yourself: did the player state any personal fact about themselves (name, family, home, allegiance)? Did they make a promise or leave a question unanswered? If yes to either, populate the matching field — do not just fold it into summary and move on."
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
            "Mandatory check, not optional: any new fact the player stated about themselves that should be remembered indefinitely — their name, family members (spouse, children, pets), hometown, allegiance, and similar. Example: the player says 'I have a dog named Noah, a wife named Wakako, and a son named Leo' — that is three separate durable facts to add, even though you'll also mention it in summary. Do not include anything about the NPC, or anything already covered by quest or reputation state. Only omit if the player truly revealed nothing new about themselves this conversation."
        )]
            IReadOnlyCollection<string> durableFactsAdded = null!,
        [Description(
            "The numbers of durable facts, from this conversation's start_conversation DurableFacts list, that the player has now contradicted and should be retracted. Omit if none were contradicted."
        )]
            IReadOnlyCollection<int> durableFactsRemoved = null!,
        [Description(
            "Mandatory check, not optional: any new unresolved thread with the player — a promise made, a question left unanswered, something to circle back to next time. Only for things with a natural resolution, not permanent facts. Only omit if nothing was left pending this conversation."
        )]
            IReadOnlyCollection<string> openThreadsAdded = null!,
        [Description(
            "The numbers of open threads, from this conversation's start_conversation OpenThreads list, that were resolved or addressed this conversation. Omit if none were resolved."
        )]
            IReadOnlyCollection<int> openThreadsRemoved = null!,
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
