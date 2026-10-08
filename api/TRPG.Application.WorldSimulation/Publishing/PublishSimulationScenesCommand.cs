using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Scenes.Commands;
using TRPG.Application.WorldSimulation;
using TRPG.Application.WorldSimulation.Queries;
using TRPG.Domain;

namespace TRPG.Application.WorldSimulation.Publishing;

public class PublishSimulationScenesCommand
{
    public required Guid WorldId { get; init; }
    public required GameInstant GameTime { get; init; }
    public required IReadOnlyDictionary<
        Guid,
        IReadOnlySet<Guid>
    > ChangedCreatureIdsByLocationId { get; init; }
}

internal class PublishSimulationScenesCommandHandler(
    IQueryHandler<
        GetActiveLocationPlayersQuery,
        IReadOnlyCollection<ActiveLocationPlayer>
    > getActiveLocationPlayers,
    ICommandHandler<PublishSceneCreatureChangesCommand> publishSceneCreatureChanges
) : ICommandHandler<PublishSimulationScenesCommand>
{
    public async Task Handle(
        PublishSimulationScenesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var players = await getActiveLocationPlayers.Handle(
            new GetActiveLocationPlayersQuery { WorldId = command.WorldId },
            cancellationToken
        );

        foreach (var player in players)
        {
            if (
                !command.ChangedCreatureIdsByLocationId.TryGetValue(
                    player.LocationId,
                    out var creatureIds
                )
            )
            {
                continue;
            }

            await publishSceneCreatureChanges.Handle(
                new PublishSceneCreatureChangesCommand
                {
                    WorldId = command.WorldId,
                    PlayerId = player.PlayerId,
                    LocationId = player.LocationId,
                    GameTime = command.GameTime,
                    CreatureIds = creatureIds,
                },
                cancellationToken
            );
        }
    }
}
