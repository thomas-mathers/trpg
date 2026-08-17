using TRPG.Application.Creatures.Events;
using TRPG.Creatures.ClientModels;

namespace TRPG.GameSessions.Hubs;

internal sealed class CharacterLevelUpEventFormatter
    : GameClientEventFormatter<CharacterLevelUpEvent>
{
    protected override Task Dispatch(IGameClient client, CharacterLevelUpEvent gameEvent) =>
        client.CharacterLevelUp(new CharacterLevelUp(gameEvent.Level));
}
