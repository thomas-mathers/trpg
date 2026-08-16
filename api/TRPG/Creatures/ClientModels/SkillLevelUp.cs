using TRPG.Abilities.Responses;

namespace TRPG.Creatures.ClientModels;

public record SkillLevelUp(
    Skill Skill,
    int Level,
    int CharacterExperienceCurrent,
    int CharacterExperienceToNextLevel
);
