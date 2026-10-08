using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetSimulatableCreaturesQuery
{
    public required Guid WorldId { get; init; }
    public IReadOnlyCollection<Guid>? CreatureIds { get; init; }
}

public record SimulatableCreature(
    Guid Id,
    Guid LocationId,
    float MovementSpeed,
    Profession? Profession
);

internal class GetSimulatableCreaturesQueryHandler(ICreaturesDbContext context)
    : IQueryHandler<GetSimulatableCreaturesQuery, IReadOnlyCollection<SimulatableCreature>>
{
    public async Task<IReadOnlyCollection<SimulatableCreature>> Handle(
        GetSimulatableCreaturesQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var creatureIds = query.CreatureIds?.ToArray();
        return await context
            .Creatures.AsNoTracking()
            .Where(creature =>
                creature.WorldId == query.WorldId
                && creature.Condition != CreatureCondition.Dead
                && !creature.IsEngaged
                && !creature.IsRestrained
                && creature.MovementSpeed > 0
                && (creatureIds == null || creatureIds.Contains(creature.Id))
            )
            .Select(creature => new SimulatableCreature(
                creature.Id,
                creature.LocationId,
                creature.MovementSpeed,
                creature.Profession
            ))
            .ToArrayAsync(cancellationToken);
    }
}
