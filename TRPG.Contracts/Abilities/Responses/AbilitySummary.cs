namespace TRPG.Contracts.Abilities.Responses;

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

public enum AbilityCategory
{
    Offensive,
    Support,
}

public record AbilitySummary(
    string Name,
    Skill Skill,
    string Description,
    int ApCost,
    int MpCost,
    int Cooldown,
    AbilityCategory Category
);
