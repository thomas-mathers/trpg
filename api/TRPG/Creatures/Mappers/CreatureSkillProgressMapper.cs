using TRPG.Abilities.Mappers;
using TRPG.Application.Creatures.Queries;
using TRPG.Creatures.Responses;
using TRPG.Domain.Models;

namespace TRPG.Creatures.Mappers;

internal static class CreatureSkillProgressMapper
{
    public static SkillProgressSummary ToSummary(this CreatureSkillProgress progress) =>
        new(
            progress.Skill.ToResponse(),
            progress.Level,
            progress.ExperienceCurrent,
            progress.ExperienceToNextLevel
        );
}
