using TRPG.Application.Creatures.Events;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class PlayerCorrectedEventMapper : GameClientEventMapper<PlayerCorrectedEvent>
{
    protected override IGameClientCall Map(PlayerCorrectedEvent gameEvent) =>
        new GameClientCall<PlayerCorrectedPayload>(
            new PlayerCorrectedPayload(
                gameEvent.PlayerId,
                gameEvent.LocationId,
                gameEvent.X,
                gameEvent.Y
            ),
            static (client, arguments) => client.PlayerCorrected(arguments)
        );
}
