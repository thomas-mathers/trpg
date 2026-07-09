using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Queries;

internal class GetAllStatesByCountryIdQuery
{
    public required Guid CountryId { get; init; }
}

internal class GetAllStatesByCountryIdQueryHandler(TrpgDbContext context)
{
    public async Task<IReadOnlyCollection<State>> Handle(
        GetAllStatesByCountryIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var list = await context
            .States.AsNoTracking()
            .Where(r => r.CountryId == query.CountryId)
            .ToArrayAsync(cancellationToken);
        return list;
    }
}
