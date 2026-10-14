using TRPG.Domain.Models;

namespace TRPG.Application.Abilities;

public readonly record struct AbilityRange(double Min, double Max)
{
    public static AbilityRange DefaultFor(Skill skill) =>
        skill switch
        {
            Skill.Archery => new AbilityRange(2, 25),
            Skill.Destruction or Skill.Illusion or Skill.Alteration or Skill.Restoration =>
                new AbilityRange(0, 20),
            _ => new AbilityRange(0, 2.5),
        };

    public bool Contains(double distance) => distance >= Min && distance <= Max;
}
