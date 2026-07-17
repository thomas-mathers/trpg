using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Tools;
using TRPG.Application.Conversations.Commands;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;

namespace TRPG.Application.Conversations.Tools;

internal class EndConversationTool(
    GameTurnContext turnContext,
    SetConversationSummaryCommandHandler setConversationSummary,
    GetGameSessionQueryHandler getGameSession,
    UpdateGameSessionCommandHandler updateGameSession,
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

        var snapshot = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = turnContext.SessionId },
            cancellationToken
        );
        if (!snapshot.OpenConversationCreatureIdsByName.TryGetValue(npcName, out var npcId))
        {
            return new ToolError(
                $"No open conversation with '{npcName}'. Call start_conversation first."
            );
        }

        await setConversationSummary.Handle(
            new SetConversationSummaryCommand
            {
                WorldId = turnContext.WorldId,
                CreatureId = turnContext.PlayerId,
                NpcId = npcId,
                Summary = summary,
            },
            cancellationToken
        );
        snapshot.OpenConversationCreatureIdsByName.Remove(npcName);
        await updateGameSession.Handle(
            new UpdateGameSessionCommand
            {
                SessionId = turnContext.SessionId,
                OpenConversationCreatureIdsByName = snapshot.OpenConversationCreatureIdsByName,
            },
            cancellationToken
        );

        var result = new { Saved = true };
        logger.LogInformation(
            "[perf] [end_conversation] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(result, ToolJsonOptions.Options)
        );
        return result;
    }
}
