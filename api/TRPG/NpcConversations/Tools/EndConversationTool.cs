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
        CancellationToken cancellationToken
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
