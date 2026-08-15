using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Application.CreatureFormulas;
using TRPG.Data;

namespace TRPG.Application.Creatures.Queries;

public class GetTotalCharacterXpFromSkillsQuery
{
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
}

public class GetTotalCharacterXpFromSkillsQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetTotalCharacterXpFromSkillsQuery, IReadOnlyDictionary<Guid, int>>
{
    public async Task<IReadOnlyDictionary<Guid, int>> Handle(
        GetTotalCharacterXpFromSkillsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var levelsByCreature = await context
            .CreatureSkills.AsNoTracking()
            .Where(s => query.CreatureIds.Contains(s.CreatureId))
            .Select(s => new { s.CreatureId, s.Level })
            .ToArrayAsync(cancellationToken);

        return levelsByCreature
            .GroupBy(x => x.CreatureId)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => SkillFormulas.CalculateExperienceFromSkillLevel(x.Level))
            );
    }
}
