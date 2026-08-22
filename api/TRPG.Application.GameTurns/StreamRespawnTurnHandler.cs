using System.Text.Json;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameTurns.Events;
using TRPG.Application.Scenes.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class StreamRespawnTurnHandler(
    GameTurnStreamer streamer,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    ICommandHandler<ResolvePlayerRespawnCommand, PlayerRespawnFact> resolvePlayerRespawn,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
    IGameClientEventSink gameEvents
)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        CancellationToken cancellationToken = default
    ) => streamer.StreamTurn(session, ct => ResolveTurn(session, ct), cancellationToken);

    private async Task<GameTurnPrompt> ResolveTurn(
        GameTurnSession session,
        CancellationToken cancellationToken
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = session.PlayerId },
            cancellationToken
        );
        if (player?.State != CreatureState.Dead)
        {
            return new GameTurnPrompt.Reply("There's nothing to respawn from right now.");
        }

        var fact = await resolvePlayerRespawn.Handle(
            new ResolvePlayerRespawnCommand
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
            },
            cancellationToken
        );

        var refreshed = await refreshScene.Handle(
            new RefreshSceneCommand
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                SessionId = session.SessionId,
            },
            cancellationToken
        );
        gameEvents.Enqueue(new SceneUpdatedEvent(refreshed.Scene));

        return new GameTurnPrompt.Narrate(BuildNarrationPrompt(fact), IncludeTools: false);
    }

    private static string BuildNarrationPrompt(PlayerRespawnFact fact)
    {
        var json = JsonSerializer.Serialize(
            fact,
            TRPG.Application.Common.Serialization.TrpgJsonOptions.Default
        );

        return $"""
            The player died and has been revived at a temple in {fact.TempleCityName}. Result: {json}.
            Narrate the player waking in the temple's Sanctuary, healed of their wounds. Only describe
            a cleric tending to them if IsClericPresent is true (in which case ClericName is who) —
            otherwise describe the Sanctuary as quiet and unattended. Briefly acknowledge that their
            belongings were left behind at {fact.DeathLocationName} and must be recovered. Do not call
            any tools.
            """;
    }
}
