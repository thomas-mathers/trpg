using TRPG.Contracts.Abilities.Responses;

namespace TRPG.Contracts.Creatures.Responses;

public record SkillProgressSummary(
    Skill Skill,
    int Level,
    int ExperienceCurrent,
    int ExperienceToNextLevel
);
