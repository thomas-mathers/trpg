using TRPG.Application.Common.Queries;
using TRPG.Application.Factions.Queries;

namespace TRPG.Application.Encounters;

// A break-in wrongs whoever owns the building and always the city whose law protects it, so a
// building nobody owns still has someone to answer to.
internal class WrongedFactionResolver(
    LocationCityResolver locationCity,
    IQueryHandler<GetCityFactionByCityIdQuery, Guid?> getCityFactionByCityId
)
{
    public async Task<List<Guid>> Resolve(
        Guid locationId,
        Guid? ownerFactionId,
        CancellationToken cancellationToken = default
    )
    {
        var factionIds = new List<Guid>();
        if (ownerFactionId is { } owner)
        {
            factionIds.Add(owner);
        }

        var cityId = await locationCity.Resolve(locationId, cancellationToken);
        if (cityId is not { } city)
        {
            return factionIds;
        }

        var cityFactionId = await getCityFactionByCityId.Handle(
            new GetCityFactionByCityIdQuery { CityId = city },
            cancellationToken
        );
        if (cityFactionId is { } cityFaction && !factionIds.Contains(cityFaction))
        {
            factionIds.Add(cityFaction);
        }

        return factionIds;
    }
}
