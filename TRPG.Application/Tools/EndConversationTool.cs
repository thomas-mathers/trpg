using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Conversations.Commands;
using TRPG.Application.Game;

namespace TRPG.Application.Tools;

internal class EndConversationTool(
    GameSession session,
    SetConversationSummaryCommandHandler setConversationSummary,
    ILogger<EndConversationTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("end_conversation")]
    [Description(
        "Call this when a conversation with someone winds down or the topic changes significantly, to save a summary of what was discussed so you can recall it next time you speak with them."
    )]
    private async Task<object?> InvokeAsync(
        [Description(
            "The exact Name of the person you spoke with, copied verbatim from the most recent look or move result."
        )]
            string npcName,
        [Description(
            "A concise, third-person, factual summary of what was discussed — replaces any previous summary for this person."
        )]
            string summary,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("[end_conversation] npcName={NpcName}", npcName);
        var stopwatch = Stopwatch.StartNew();

        if (!session.OpenConversationCreatureIdsByName.TryGetValue(npcName, out var npcId))
        {
            return new
            {
                Error = $"No open conversation with '{npcName}'. Call start_conversation first.",
            };
        }

        await setConversationSummary.Handle(
            new SetConversationSummaryCommand
            {
                WorldId = session.WorldId,
                CreatureId = session.PlayerId,
                NpcId = npcId,
                Summary = summary,
            },
            cancellationToken
        );
        session.OpenConversationCreatureIdsByName.Remove(npcName);

        var result = new { Saved = true };
        logger.LogInformation(
            "[perf] [end_conversation] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(result, ToolJsonOptions.Options)
        );
        return result;
    }
}
