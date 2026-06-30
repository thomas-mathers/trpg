namespace TRPG.Models;

internal enum TargetType {
    Single,
    Aoe,
    Self
}

internal enum Skill {
    Swordsmanship,
    Stealth,
    Spellcasting,
    Archery,
    Devotion,
    Warfare
}

internal class Ability {
    public float? AoeRadius { get; init; }
    public int Cooldown { get; init; }
    public int Cost { get; init; }
    public string Description { get; init; } = "";
    public string Name { get; init; } = "";
    public int RequiredSkillLevel { get; init; }
    public Skill Skill { get; init; }
    public TargetType TargetType { get; init; }
}