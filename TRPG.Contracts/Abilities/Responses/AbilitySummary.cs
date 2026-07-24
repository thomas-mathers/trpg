namespace TRPG.Contracts.Abilities.Responses;

public enum Skill
{
    Melee,
    Unarmed,
    Stealth,
    Spellcasting,
    Archery,
    Devotion,
    Warfare,
    General,
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
