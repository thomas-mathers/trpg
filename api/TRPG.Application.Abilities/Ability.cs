using TRPG.Domain.Models;

namespace TRPG.Application.Abilities;

public enum GearRequirement
{
    None,
    MeleeWeapon,
    RangedWeapon,
    CasterWeapon,
    Shield,
}

public class Ability
{
    public int ApCost { get; init; }
    public int Cooldown { get; init; }
    public string Description { get; init; } = "";
    public int MpCost { get; init; }
    public string Name { get; init; } = "";
    public int RequiredSkillLevel { get; init; }
    public Skill Skill { get; init; } = Skill.General;
    public GearRequirement GearRequirement { get; init; } = GearRequirement.None;
    public List<string> Prerequisites { get; init; } = [];
}
