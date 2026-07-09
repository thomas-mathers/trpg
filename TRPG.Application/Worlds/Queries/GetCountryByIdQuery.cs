using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Worlds.Queries;

internal class GetCountryByIdQuery
{
    public required Guid Id { get; init; }
}

internal class GetCountryByIdQueryHandler(TrpgDbContext context)
{
    public async Task<Country?> Handle(
        GetCountryByIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context.Countries.FindAsync([query.Id], cancellationToken);
    }
}
