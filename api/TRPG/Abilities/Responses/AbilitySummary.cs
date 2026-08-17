namespace TRPG.Abilities.Responses;

[Tapper.TranspilationSource]
public enum Skill
{
    Melee,
    Unarmed,
    Sneak,
    Destruction,
    Illusion,
    Archery,
    Restoration,
    Alteration,
    General,
    Blocking,
}

[Tapper.TranspilationSource]
public enum AbilityCategory
{
    Offensive,
    Support,
}

[Tapper.TranspilationSource]
public record AbilitySummary(
    string Name,
    Skill Skill,
    string Description,
    int ApCost,
    int MpCost,
    int Cooldown,
    AbilityCategory Category,
    int RequiredSkillLevel,
    IReadOnlyCollection<string> Prerequisites
);
