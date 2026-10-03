using TRPG.Application.Common.Events;

namespace TRPG.Application.Creatures.Events;

public record CharacterLevelUpEvent(Guid WorldId, int Level) : GameClientEvent(WorldId);
