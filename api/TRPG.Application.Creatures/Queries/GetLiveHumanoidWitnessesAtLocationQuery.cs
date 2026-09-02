using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Queries;

public class GetLiveHumanoidWitnessesAtLocationQuery
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
    public required Guid ExcludeCreatureId { get; init; }
}

public record LiveHumanoidWitness(Guid Id, string Name);

internal class GetLiveHumanoidWitnessesAtLocationQueryHandler(ICreaturesDbContext context)
    : IQueryHandler<
        GetLiveHumanoidWitnessesAtLocationQuery,
        IReadOnlyCollection<LiveHumanoidWitness>
    >
{
    public async Task<IReadOnlyCollection<LiveHumanoidWitness>> Handle(
        GetLiveHumanoidWitnessesAtLocationQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Creatures.AsNoTracking()
            .Where(creature =>
                creature.WorldId == query.WorldId
                && creature.LocationId == query.LocationId
                && creature.State != CreatureState.Dead
                && creature.State != CreatureState.Sleeping
                && creature.Id != query.ExcludeCreatureId
                && CreatureTypes.Humanoid.AsEnumerable().Contains(creature.CreatureType)
            )
            .Select(creature => new LiveHumanoidWitness(creature.Id, creature.Name))
            .ToArrayAsync(cancellationToken);
}
