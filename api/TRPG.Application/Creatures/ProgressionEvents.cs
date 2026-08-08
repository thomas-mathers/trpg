using TRPG.Application.GameSessions;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures;

public record SkillLevelUpEvent(
    Skill Skill,
    int Level,
    int CharacterExperienceCurrent,
    int CharacterExperienceToNextLevel
) : GameTurnEvent
{
    public override string MethodName => "SkillLevelUp";
    public override object? Payload => this;
}

public record CharacterLevelUpEvent(int Level) : GameTurnEvent
{
    public override string MethodName => "CharacterLevelUp";
    public override object? Payload => this;
}
