using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetRegeneratingCreaturesQuery
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<Guid> LocationIds { get; init; }
    public IReadOnlyCollection<Guid> ExcludedCreatureIds { get; init; } = [];
}

internal class GetRegeneratingCreaturesQueryHandler(ICreaturesDbContext context)
    : IQueryHandler<GetRegeneratingCreaturesQuery, IReadOnlyCollection<CreatureVitalState>>
{
    public async Task<IReadOnlyCollection<CreatureVitalState>> Handle(
        GetRegeneratingCreaturesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Creatures.AsNoTracking()
            .Where(creature =>
                creature.WorldId == query.WorldId
                && query.LocationIds.AsEnumerable().Contains(creature.LocationId)
                && creature.Condition != CreatureCondition.Dead
                && (
                    creature.CurrentHp < creature.MaximumHp
                    || creature.CurrentAp < creature.MaximumAp
                    || creature.CurrentMp < creature.MaximumMp
                )
                && !query.ExcludedCreatureIds.AsEnumerable().Contains(creature.Id)
            )
            .Select(creature => new CreatureVitalState(
                new CreatureVitals(
                    creature.Id,
                    creature.CurrentHp,
                    creature.MaximumHp,
                    creature.CurrentAp,
                    creature.MaximumAp,
                    creature.CurrentMp,
                    creature.MaximumMp
                ),
                creature.LocationId,
                creature.LastRegenGameTime
            ))
            .ToArrayAsync(cancellationToken);
    }
}
