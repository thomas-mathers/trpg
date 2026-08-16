using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Queries;

public class GetItemByIdQuery
{
    public required Guid ItemId { get; init; }
    public required Guid WorldId { get; init; }
}

internal class GetItemByIdQueryHandler(TrpgDbContext context)
    : IQueryHandler<GetItemByIdQuery, Item?>
{
    public async Task<Item?> Handle(
        GetItemByIdQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Items.AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == query.ItemId && item.WorldId == query.WorldId,
                cancellationToken
            );
    }
}
