using TRPG.Abilities.Mappers;
using TRPG.Application.Creatures.Queries;
using TRPG.Contracts.Creatures.Responses;
using TRPG.Data.Models;

namespace TRPG.Creatures.Mappers;

internal static class CreatureSkillProgressMapper
{
    public static SkillProgressSummary ToSummary(this CreatureSkillProgress progress) =>
        new(
            progress.Skill.ToContract(),
            progress.Level,
            progress.ExperienceCurrent,
            progress.ExperienceToNextLevel
        );
}
