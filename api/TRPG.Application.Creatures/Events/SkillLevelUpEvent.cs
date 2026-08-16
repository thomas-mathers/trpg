using TRPG.Application.Common.Events;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Events;

internal record SkillLevelUpEvent(
    Skill Skill,
    int Level,
    int CharacterExperienceCurrent,
    int CharacterExperienceToNextLevel
) : GameClientEvent
{
    public override string MethodName => "SkillLevelUp";
    public override object? Payload => this;
}
