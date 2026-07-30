using TRPG.Data.Models;

namespace TRPG.Application.Balance;

// A directly-specified combatant for balance simulation - no procedural generation, no
// database. MaximumHp/Ap/Mp on Attributes are ignored and recomputed via StatFormulas, so
// callers only need to specify the stats that actually drive those formulas (Endurance,
// Stamina, Mana, etc.) rather than pre-computing the derived maximums themselves.
public record CombatantSpec(
    string Name,
    Attributes Attributes,
    IReadOnlyDictionary<Skill, int> SkillLevels
);
