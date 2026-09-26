using TRPG.Application.Common.Commands;
using TRPG.Application.Encounters.Commands;
using TRPG.Domain;

namespace TRPG.Application.LocationSimulation.Commands;

public class SyncActiveLocationRoutinesCommand
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<ActiveLocationPlayer> Players { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class SyncActiveLocationRoutinesCommandHandler(
    ICommandHandler<SyncLocationRoutinesCommand, SyncLocationRoutinesResult> syncLocationRoutines,
    ICommandHandler<EvaluateAmbientEncounterCommand> evaluateAmbientEncounter
) : ICommandHandler<SyncActiveLocationRoutinesCommand>
{
    public async Task Handle(
        SyncActiveLocationRoutinesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var player in ActiveLocations.Distinct(command.Players))
        {
            var routines = await syncLocationRoutines.Handle(
                new SyncLocationRoutinesCommand
                {
                    WorldId = command.WorldId,
                    PlayerId = player.PlayerId,
                    LocationId = player.LocationId,
                    PlayerLevel = player.PlayerLevel,
                    GameTime = command.GameTime,
                },
                cancellationToken
            );

            // Only a group that just appeared can ambush a player who is already standing here.
            await evaluateAmbientEncounter.Handle(
                new EvaluateAmbientEncounterCommand
                {
                    WorldId = command.WorldId,
                    PlayerId = player.PlayerId,
                    SpawnedGroupIds = routines.SpawnedEncounterGroupIds,
                    GameTime = command.GameTime,
                },
                cancellationToken
            );
        }
    }
}
