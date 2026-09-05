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

internal class StreamGuardEncounterActionTurnHandler(
    GameTurnStreamer streamer,
    IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
    ICommandHandler<
        ResolveGuardEncounterActionCommand,
        GuardEncounterResolutionFact
    > resolveGuardEncounterAction,
    ICommandHandler<RefreshSceneCommand, RefreshSceneResult> refreshScene,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    IGameClientEventSink gameEvents
)
    : EncounterActionTurnHandlerBase<
        GuardEncounter,
        GuardEncounterAction,
        GuardEncounterResolutionFact
    >(streamer, getActiveEncounter, refreshScene, getPlaytime, gameEvents)
{
    protected override async Task<GuardEncounterResolutionFact> Resolve(
        GameTurnSession session,
        GuardEncounter encounter,
        GuardEncounterAction action,
        CancellationToken cancellationToken
    ) =>
        await resolveGuardEncounterAction.Handle(
            new ResolveGuardEncounterActionCommand
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
        GuardEncounterResolutionFact resolution
    ) => new GuardEncounterResolvedEvent(resolution);

    protected override string BuildNarrationPrompt(
        GuardEncounterAction action,
        GuardEncounterResolutionFact resolution
    )
    {
        var json = JsonSerializer.Serialize(
            resolution,
            TRPG.Application.Common.Serialization.TrpgJsonOptions.Default
        );

        if (action is GoToJailEncounterAction)
        {
            return $"""
                The player chose to go to jail with {resolution.GuardName}. Result: {json}.
                Narrate ONLY the guard escorting the player into a locked cell and stating the
                sentence length. The player has NOT served their time or been released yet — do
                not narrate the sentence passing, the player leaving, or any time skip forward.
                End the narration with the player now confined in the cell. Do not call any tools.
                """;
        }

        var actionDescription = DescribeAction(action);

        return $"""
            The player chose to {actionDescription} with {resolution.GuardName}. Result: {json}.
            Narrate the outcome vividly based on this result. Do not call any tools.
            """;
    }

    private static string DescribeAction(GuardEncounterAction action) =>
        action switch
        {
            PayFineEncounterAction => "pay the fine",
            GoToJailEncounterAction => "go to jail",
            ResistArrestEncounterAction => "resist arrest",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
}
