using System.Text.Json;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Events;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.Scenes.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class StreamTheftEncounterActionTurnHandler(
    GameTurnStreamer streamer,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    ICommandHandler<
        ResolveTheftEncounterActionCommand,
        TheftEncounterResolutionFact
    > resolveTheftEncounterAction,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
    IGameClientEventSink gameEvents
)
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        TheftEncounterAction action,
        CancellationToken cancellationToken = default
    ) => streamer.StreamTurn(session, ct => ResolveTurn(session, action, ct), cancellationToken);

    private async Task<GameTurnPrompt> ResolveTurn(
        GameTurnSession session,
        TheftEncounterAction action,
        CancellationToken cancellationToken
    )
    {
        var encounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = session.PlayerId },
            cancellationToken
        );

        if (
            encounter is not TheftEncounter theftEncounter
            || theftEncounter.WorldId != session.WorldId
        )
        {
            return new GameTurnPrompt.Reply("There's no theft encounter to resolve right now.");
        }

        var resolution = await resolveTheftEncounterAction.Handle(
            new ResolveTheftEncounterActionCommand
            {
                SessionId = session.SessionId,
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                Action = action,
                EncounterId = theftEncounter.Id,
            },
            cancellationToken
        );

        gameEvents.Enqueue(new TheftEncounterResolvedEvent(resolution));

        await refreshScene.Handle(
            new RefreshSceneCommand
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                SessionId = session.SessionId,
            },
            cancellationToken
        );

        return new GameTurnPrompt.Narrate(
            BuildNarrationPrompt(action, resolution),
            IncludeTools: false
        );
    }

    private static string BuildNarrationPrompt(
        TheftEncounterAction action,
        TheftEncounterResolutionFact resolution
    ) =>
        action switch
        {
            FightTheftEncounterAction =>
                $"The player chose to fight after {resolution.ConfrontingName} caught them stealing. Narrate only the confrontation erupting into violence — {resolution.ConfrontingName} readying to defend themselves and the fight beginning. The fight has not been resolved yet: do not describe who wins, who is hurt, or how it ends. Do not call any tools.",
            ApologizeTheftEncounterAction =>
                $"The player chose to apologize after {resolution.ConfrontingName} caught them stealing. Result: {JsonSerializer.Serialize(resolution, TRPG.Application.Common.Serialization.TrpgJsonOptions.Default)}. Narrate the outcome vividly based on this result. Do not call any tools.",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
}
