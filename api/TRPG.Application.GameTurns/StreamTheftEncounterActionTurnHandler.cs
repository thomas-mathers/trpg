using System.Text.Json;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Events;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameTurns.Events;
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

        return new GameTurnPrompt.Narrate(
            $"The player chose to {DescribeAction(action)} after {resolution.ConfrontingName} caught them stealing. Result: {JsonSerializer.Serialize(resolution, TRPG.Application.Common.Serialization.TrpgJsonOptions.Default)}. Narrate the outcome vividly based on this result. Do not call any tools.",
            IncludeTools: false
        );
    }

    private static string DescribeAction(TheftEncounterAction action) =>
        action switch
        {
            ApologizeTheftEncounterAction => "apologize",
            FightTheftEncounterAction => "fight",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
}
