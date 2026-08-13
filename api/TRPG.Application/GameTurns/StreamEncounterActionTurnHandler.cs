using System.Text.Json;
using TRPG.Application.Combat;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Tools;
using TRPG.Application.GameSessions;
using TRPG.Application.Scenes;
using TRPG.Application.Worlds.Encounters;
using TRPG.Application.Worlds.Encounters.Commands;
using TRPG.Application.Worlds.Encounters.Queries;
using TRPG.Contracts.Encounters.Requests;

namespace TRPG.Application.GameTurns;

internal class StreamEncounterActionTurnHandler(
    GameTurnStreamer streamer,
    GetActiveEncounterQueryHandler getActiveEncounter,
    ResolveEncounterActionCommandHandler resolveEncounterAction,
    IGameClientEventSink gameEvents
)
{
    public IAsyncEnumerable<string> Handle(
        GameSessionIdentity session,
        PlayerEncounterAction action,
        CancellationToken cancellationToken = default
    ) => streamer.StreamTurn(session, ct => ResolveTurn(session, action, ct), cancellationToken);

    private async Task<GameTurnPrompt> ResolveTurn(
        GameSessionIdentity session,
        PlayerEncounterAction action,
        CancellationToken cancellationToken
    )
    {
        var encounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = session.PlayerId },
            cancellationToken
        );

        if (encounter == null)
        {
            return new GameTurnPrompt.Reply("There's no encounter to resolve right now.");
        }

        var resolution = await resolveEncounterAction.Handle(
            new ResolveEncounterActionCommand
            {
                SessionId = session.SessionId,
                WorldId = session.WorldId,
                PlayerId = session.PlayerId,
                Action = action,
                EncounterId = encounter.Id,
                EncounterGroupId = encounter.EncounterGroupId,
                EncounterLocationId = encounter.LocationId,
                ArrivalOriginLocationId = encounter.ArrivalOriginLocationId,
            },
            cancellationToken
        );

        EnqueueEncounterResolutionEvents(resolution);

        return new GameTurnPrompt.Narrate(
            $"The player chose to {DescribeAction(resolution.ActionKind)} the {resolution.Fact.FactionName} encounter. Result: {JsonSerializer.Serialize(resolution.Fact, ToolJsonOptions.Options)}. Narrate the outcome vividly based on this result. Do not call any tools.",
            IncludeTools: false
        );
    }

    private void EnqueueEncounterResolutionEvents(EncounterActionResolution resolution)
    {
        gameEvents.Enqueue(new EncounterResolvedEvent(resolution.Fact));

        if (resolution.UpdatedScene != null)
        {
            gameEvents.Enqueue(
                new SceneUpdatedEvent(
                    SceneSnapshotMapper.ToSnapshot(resolution.UpdatedScene),
                    SceneUpdateReason.Moved
                )
            );
        }

        if (resolution.FightState != null)
        {
            gameEvents.Enqueue(new CombatStartedEvent(resolution.FightState));
        }
    }

    private static string DescribeAction(HostileEncounterActionKind actionKind) =>
        actionKind switch
        {
            HostileEncounterActionKind.Attack => "attack",
            HostileEncounterActionKind.Evade => "evade",
            HostileEncounterActionKind.Retreat => "retreat from",
            _ => throw new ArgumentOutOfRangeException(nameof(actionKind)),
        };
}
