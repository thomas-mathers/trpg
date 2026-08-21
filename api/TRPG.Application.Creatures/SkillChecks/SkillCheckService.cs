using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Creatures.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures;

public interface IChanceRoller
{
    bool Roll(float chance);
}

internal sealed class ChanceRoller : IChanceRoller
{
    public bool Roll(float chance) => Random.Shared.NextDouble() < chance;
}

public class SkillCheckService(
    IQueryHandler<
        GetCreatureSkillsQuery,
        IReadOnlyCollection<CreatureSkillProgress>
    > getCreatureSkills,
    IChanceRoller chanceRoller
)
{
    public async Task<bool> Roll(
        Guid creatureId,
        Skill skill,
        SkillCheckCurve curve,
        CancellationToken cancellationToken = default
    )
    {
        var skills = await getCreatureSkills.Handle(
            new GetCreatureSkillsQuery { CreatureId = creatureId },
            cancellationToken
        );
        var skillLevel = skills.SingleOrDefault(progress => progress.Skill == skill)?.Level ?? 0;
        var chance = SkillCheckCalculator.CalculateChance(skillLevel, curve);

        return chanceRoller.Roll(chance);
    }
}
