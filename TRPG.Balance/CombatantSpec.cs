using TRPG.Data.Models;

namespace TRPG.Balance;

public record CombatantSpec(
    string Name,
    Attributes Attributes,
    IReadOnlyDictionary<Skill, int> SkillLevels
);
