using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TRPG.Application.Conversations.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Game;
using TRPG.Application.Game.Commands;
using TRPG.Application.Game.Queries;
using TRPG.Application.Tools;
using TRPG.Application.Tools.Common;

namespace TRPG.Application.Conversations.Tools;

internal record StartConversationResult(string Summary, string Biography);

internal class StartConversationTool(
    GameTurnContext turnContext,
    GetCreatureByIdQueryHandler getCreatureById,
    GetCreatureByNameNearbyQueryHandler getCreatureByNameNearby,
    GetConversationSummaryQueryHandler getConversationSummary,
    GetGameSessionQueryHandler getGameSession,
    UpdateGameSessionCommandHandler updateGameSession,
    ILogger<StartConversationTool> logger
) : IGameTool
{
    public Delegate Invoke => InvokeAsync;

    [DisplayName("start_conversation")]
    [Description(
        "Call this when you begin talking to someone, to remember what was discussed the last time you spoke with them and to learn their personality, background, and manner of speech. Returns an empty summary if you've never spoken before — use the biography to voice them consistently regardless."
    )]
    private async Task<object?> InvokeAsync(
        [Description(
            "The exact Name of the person you're speaking with, copied verbatim from the most recent look or move result."
        )]
            string npcName,
        CancellationToken cancellationToken
    )
    {
        logger.LogInformation("[start_conversation] npcName={NpcName}", npcName);
        var stopwatch = Stopwatch.StartNew();

        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = turnContext.PlayerId },
            cancellationToken
        );
        var npc = await getCreatureByNameNearby.Handle(
            new GetCreatureByNameNearbyQuery
            {
                WorldId = turnContext.WorldId,
                Player = player!,
                Name = npcName,
            },
            cancellationToken
        );

        if (npc == null)
        {
            return new ToolError(
                $"No one named '{npcName}' found nearby. Call look to see who's around."
            );
        }

        var snapshot = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = turnContext.SessionId },
            cancellationToken
        );
        if (snapshot.OpenConversationCreatureIdsByName.ContainsKey(npc.Name))
        {
            return new ToolError(
                $"You are already in conversation with {npcName}; no need to call this again for them. If the dialogue has turned to someone else, call lookup instead."
            );
        }

        var summary = await getConversationSummary.Handle(
            new GetConversationSummaryQuery { CreatureId = player!.Id, NpcId = npc.Id },
            cancellationToken
        );

        snapshot.OpenConversationCreatureIdsByName[npc.Name] = npc.Id;
        await updateGameSession.Handle(
            new UpdateGameSessionCommand
            {
                SessionId = turnContext.SessionId,
                OpenConversationCreatureIdsByName = snapshot.OpenConversationCreatureIdsByName,
            },
            cancellationToken
        );

        var result = new StartConversationResult(summary, npc.Biography);
        logger.LogInformation(
            "[perf] [start_conversation] result in {ElapsedMs}ms: {Result}",
            stopwatch.ElapsedMilliseconds,
            JsonSerializer.Serialize(result, ToolJsonOptions.Options)
        );
        return result;
    }
}
