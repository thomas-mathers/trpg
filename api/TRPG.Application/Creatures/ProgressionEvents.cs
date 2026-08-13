using TRPG.Application.Common.Events;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures;

public record SkillLevelUpEvent(
    Skill Skill,
    int Level,
    int CharacterExperienceCurrent,
    int CharacterExperienceToNextLevel
) : GameClientEvent
{
    public override string MethodName => "SkillLevelUp";
    public override object? Payload => this;
}

public record CharacterLevelUpEvent(int Level) : GameClientEvent
{
    public override string MethodName => "CharacterLevelUp";
    public override object? Payload => this;
}
