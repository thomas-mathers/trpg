using TRPG.Application.Common.Commands;
using TRPG.Application.Encounters.Commands;
using TRPG.Domain;

namespace TRPG.Application.WorldSimulation.Commands;

public class SyncActiveFightEffectsCommand
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<ActiveLocationPlayer> Players { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class SyncActiveFightEffectsCommandHandler(
    ICommandHandler<AdvanceFightEffectsCommand> advanceFightEffects
) : ICommandHandler<SyncActiveFightEffectsCommand>
{
    public async Task Handle(
        SyncActiveFightEffectsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        foreach (var player in command.Players)
        {
            await advanceFightEffects.Handle(
                new AdvanceFightEffectsCommand
                {
                    WorldId = command.WorldId,
                    PlayerId = player.PlayerId,
                    GameTime = command.GameTime,
                },
                cancellationToken
            );
        }
    }
}
