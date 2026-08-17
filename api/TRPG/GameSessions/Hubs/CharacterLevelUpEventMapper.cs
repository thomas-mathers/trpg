using TRPG.Application.Creatures.Events;
using TRPG.Creatures.Responses;

namespace TRPG.GameSessions.Hubs;

internal sealed class CharacterLevelUpEventMapper : GameClientEventMapper<CharacterLevelUpEvent>
{
    protected override IGameClientCall Map(CharacterLevelUpEvent gameEvent) =>
        new GameClientCall<CharacterLevelUp>(
            new CharacterLevelUp(gameEvent.Level),
            static (client, arguments) => client.CharacterLevelUp(arguments)
        );
}
