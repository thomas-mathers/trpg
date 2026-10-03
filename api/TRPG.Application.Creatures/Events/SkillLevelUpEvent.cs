using TRPG.Application.Common.Events;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Events;

public record SkillLevelUpEvent(
    Guid WorldId,
    Skill Skill,
    int Level,
    int CharacterExperienceCurrent,
    int CharacterExperienceToNextLevel
) : GameClientEvent(WorldId);
