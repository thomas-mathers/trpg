using TRPG.Abilities.Responses;

namespace TRPG.Creatures.Responses;

public record SkillProgressSummary(
    Skill Skill,
    int Level,
    int ExperienceCurrent,
    int ExperienceToNextLevel
);
