using TRPG.Data.Models;

namespace TRPG.Application.Abilities;

public class Ability
{
    public int ApCost { get; init; }
    public int Cooldown { get; init; }
    public string Description { get; init; } = "";
    public int MpCost { get; init; }
    public string Name { get; init; } = "";
    public int RequiredSkillLevel { get; init; }

    // General, not Melee (the enum's default value), so an ability whose skill nobody bothered
    // to set is treated as gear-agnostic rather than silently requiring a melee weapon.
    public Skill Skill { get; init; } = Skill.General;
}
