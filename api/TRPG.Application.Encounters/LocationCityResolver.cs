using TRPG.Application.Common.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters;

// Which city's law applies somewhere. Null in the wilderness, where none does.
internal class LocationCityResolver(IQueryHandler<GetLocationByIdQuery, Location?> getLocationById)
{
    public async Task<Guid?> Resolve(Guid locationId, CancellationToken cancellationToken = default)
    {
        var location = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = locationId },
            cancellationToken
        );

        return location?.CityId;
    }
}
