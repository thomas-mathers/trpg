using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Queries;

public class GetCellByNameAtLocationQuery
{
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
    public required string Name { get; init; }
}

internal class GetCellByNameAtLocationQueryHandler(IPropsDbContext context)
    : IQueryHandler<GetCellByNameAtLocationQuery, Cell?>
{
    public async Task<Cell?> Handle(
        GetCellByNameAtLocationQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Props.OfType<Cell>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                cell =>
                    cell.WorldId == query.WorldId
                    && cell.LocationId == query.LocationId
                    && cell.Name == query.Name,
                cancellationToken
            );
}
