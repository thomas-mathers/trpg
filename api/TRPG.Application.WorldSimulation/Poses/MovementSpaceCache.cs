using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Navigation;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;

namespace TRPG.Application.WorldSimulation.Poses;

internal sealed class MovementSpaceCache
{
    private readonly Dictionary<Guid, MovementSpace> _spaces = [];

    public async Task<MovementSpace?> Find(
        Guid locationId,
        IServiceProvider services,
        CancellationToken cancellationToken
    )
    {
        if (_spaces.TryGetValue(locationId, out var cached))
        {
            return cached;
        }

        var space = await services
            .GetRequiredService<IQueryHandler<GetMovementSpaceQuery, MovementSpace?>>()
            .Handle(new GetMovementSpaceQuery { LocationId = locationId }, cancellationToken);
        if (space is not null)
        {
            _spaces[locationId] = space;
        }

        return space;
    }

    public void Invalidate(Guid locationId) => _spaces.Remove(locationId);
}
