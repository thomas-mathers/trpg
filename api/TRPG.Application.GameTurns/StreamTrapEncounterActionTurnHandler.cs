using System.Text.Json;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Events;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.GameTurns.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class StreamTrapEncounterActionTurnHandler(
    GameTurnStreamer streamer,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    ICommandHandler<
        ResolveTrapEncounterActionCommand,
        TrapEncounterResolutionFact
    > resolveTrapEncounterAction,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    IGameClientEventSink gameEvents
)
    : EncounterActionTurnHandlerBase<
        TrapEncounter,
        TrapEncounterAction,
        TrapEncounterResolutionFact
    >(streamer, getActiveEncounter, refreshScene, publishEncounterStarted, getPlaytime, gameEvents)
{
    protected override async Task<TrapEncounterResolutionFact> Resolve(
        GameTurnSession session,
        TrapEncounter encounter,
        TrapEncounterAction action,
        CancellationToken cancellationToken
    ) =>
        await resolveTrapEncounterAction.Handle(
            new ResolveTrapEncounterActionCommand
            {
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                SessionId = session.SessionId,
                Action = action,
                EncounterId = encounter.Id,
            },
            cancellationToken
        );

    protected override GameClientEvent BuildResolvedEvent(TrapEncounterResolutionFact resolution) =>
        new TrapEncounterResolvedEvent(resolution);

    protected override string BuildNarrationPrompt(
        TrapEncounterAction action,
        TrapEncounterResolutionFact resolution
    )
    {
        var json = JsonSerializer.Serialize(
            resolution,
            TRPG.Application.Common.Serialization.TrpgJsonOptions.Default
        );

        return resolution.Outcome switch
        {
            TrapEncounterResolutionOutcome.Withdrew => $"""
                The player stepped back from a {resolution.TrapKind} trap without disturbing it. Result: {json}.
                Narrate them backing away carefully; the trap is still there. Do not call any tools.
                """,
            TrapEncounterResolutionOutcome.Disarmed => $"""
                The player disarmed a {resolution.TrapKind} trap. Result: {json}.
                Narrate them carefully neutralizing it. Do not call any tools.
                """,
            TrapEncounterResolutionOutcome.Survived => $"""
                The player attempted to get past a {resolution.TrapKind} trap and succeeded. Result: {json}.
                Narrate them carefully avoiding it without triggering it. Do not call any tools.
                """,
            TrapEncounterResolutionOutcome.Fell => $"""
                The player triggered a {resolution.TrapKind} trap. Result: {json}.
                Narrate the specific manner of the fall this trap kind implies (Mechanical or Collapse means
                they fell through and it hurt; Slope means they lost their footing and slid; Water means a
                current swept them off their feet, possibly back the way they came) and them landing in
                {resolution.TargetLocationName}. Do not call any tools.
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(resolution)),
        };
    }
}
