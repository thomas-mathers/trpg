using TRPG.Application.Creatures.Events;
using TRPG.Creatures.ClientModels;

namespace TRPG.GameSessions.Hubs;

internal sealed class CharacterLevelUpEventFormatter
    : GameClientEventFormatter<CharacterLevelUpEvent>
{
    protected override GameClientMessage Format(CharacterLevelUpEvent gameEvent) =>
        new("CharacterLevelUp", new CharacterLevelUp(gameEvent.Level));
}
