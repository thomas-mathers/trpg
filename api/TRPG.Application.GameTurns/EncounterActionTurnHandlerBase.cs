using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.GameTurns.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal abstract class EncounterActionTurnHandlerBase<TEncounter, TAction, TResolution>(
    GameTurnStreamer streamer,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    IGameClientEventSink gameEvents
)
    where TEncounter : Encounter
{
    public IAsyncEnumerable<string> Handle(
        GameTurnSession session,
        TAction action,
        CancellationToken cancellationToken = default
    ) => streamer.StreamTurn(session, ct => ResolveTurn(session, action, ct), cancellationToken);

    protected abstract Task<TResolution> Resolve(
        GameTurnSession session,
        TEncounter encounter,
        TAction action,
        CancellationToken cancellationToken
    );

    protected abstract GameClientEvent BuildResolvedEvent(TResolution resolution);

    protected abstract string BuildNarrationPrompt(TAction action, TResolution resolution);

    private async Task<GameTurnPrompt> ResolveTurn(
        GameTurnSession session,
        TAction action,
        CancellationToken cancellationToken
    )
    {
        var encounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = session.PlayerId },
            cancellationToken
        );

        if (encounter is not TEncounter typedEncounter)
        {
            return new GameTurnPrompt.Reply("There's no encounter to resolve right now.");
        }

        var resolution = await Resolve(session, typedEncounter, action, cancellationToken);

        gameEvents.Enqueue(BuildResolvedEvent(resolution));

        // A resolution that relocates the player can start a fresh encounter on arrival; announce it
        // only after the resolved event so the client never sees the new one before the old one closes.
        var startedEncounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = session.PlayerId },
            cancellationToken
        );

        await publishEncounterStarted.Handle(
            new PublishEncounterStartedCommand
            {
                PlayerId = session.PlayerId,
                Encounter = startedEncounter,
            },
            cancellationToken
        );

        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = session.SessionId },
            cancellationToken
        );

        await refreshScene.Handle(
            new RefreshSceneCommand
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                Playtime = playtime,
            },
            cancellationToken
        );

        return new GameTurnPrompt.Narrate(
            BuildNarrationPrompt(action, resolution),
            IncludeTools: false
        );
    }
}
