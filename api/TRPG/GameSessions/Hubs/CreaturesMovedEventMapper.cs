using TRPG.Application.Scenes.Events;
using TRPG.GameSessions.Mappers;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class CreaturesMovedEventMapper : GameClientEventMapper<CreaturesMovedEvent>
{
    protected override IGameClientCall Map(CreaturesMovedEvent gameEvent) =>
        new GameClientCall<CreaturesMoved>(
            new CreaturesMoved(
                gameEvent.Direction.ToResponse(),
                gameEvent.Names,
                gameEvent.PlaceName
            ),
            static (client, arguments) => client.CreaturesMoved(arguments)
        );
}
