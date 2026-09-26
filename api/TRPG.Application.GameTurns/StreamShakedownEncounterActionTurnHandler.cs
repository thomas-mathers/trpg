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
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.GameTurns;

internal class StreamShakedownEncounterActionTurnHandler(
    GameTurnStreamer streamer,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    ICommandHandler<
        ResolveShakedownEncounterActionCommand,
        ShakedownEncounterResolutionFact
    > resolveShakedownEncounterAction,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted,
    IQueryHandler<GetGameTimeQuery, GameInstant> getGameTime,
    IGameClientEventSink gameEvents
)
    : EncounterActionTurnHandlerBase<
        ShakedownEncounter,
        ShakedownEncounterAction,
        ShakedownEncounterResolutionFact
    >(streamer, getActiveEncounter, refreshScene, publishEncounterStarted, getGameTime, gameEvents)
{
    protected override async Task<ShakedownEncounterResolutionFact> Resolve(
        GameTurnSession session,
        ShakedownEncounter encounter,
        ShakedownEncounterAction action,
        CancellationToken cancellationToken
    ) =>
        await resolveShakedownEncounterAction.Handle(
            new ResolveShakedownEncounterActionCommand
            {
                SessionId = session.SessionId,
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                Action = action,
                EncounterId = encounter.Id,
            },
            cancellationToken
        );

    protected override GameClientEvent BuildResolvedEvent(
        ShakedownEncounterResolutionFact resolution
    ) => new ShakedownEncounterResolvedEvent(resolution);

    protected override string BuildNarrationPrompt(
        ShakedownEncounterAction action,
        ShakedownEncounterResolutionFact resolution
    ) =>
        resolution.Outcome switch
        {
            ShakedownEncounterResolutionOutcome.Fought
            or ShakedownEncounterResolutionOutcome.IntimidateFailed
            or ShakedownEncounterResolutionOutcome.FleeFailed =>
                $"The player chose to {DescribeAction(action)} the {resolution.FactionName} toll demand, and it has erupted into a fight. Narrate only the confrontation beginning. The fight has not been resolved yet: do not describe who wins, who is hurt, or how it ends. Do not call any tools.",
            _ =>
                $"The player chose to {DescribeAction(action)} the {resolution.FactionName} toll demand. Result: {JsonSerializer.Serialize(resolution, TRPG.Application.Common.Serialization.TrpgJsonOptions.Default)}. Narrate the outcome vividly based on this result. Do not call any tools.",
        };

    private static string DescribeAction(ShakedownEncounterAction action) =>
        action switch
        {
            IntimidateEncounterAction => "intimidate",
            PayTollEncounterAction => "pay the toll demanded by",
            FightEncounterAction => "fight",
            FleeShakedownEncounterAction => "flee from",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
}
