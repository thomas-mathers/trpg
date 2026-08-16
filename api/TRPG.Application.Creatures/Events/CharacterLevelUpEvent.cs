using TRPG.Application.Common.Events;

namespace TRPG.Application.Creatures.Events;

public record CharacterLevelUpEvent(int Level) : GameClientEvent;
