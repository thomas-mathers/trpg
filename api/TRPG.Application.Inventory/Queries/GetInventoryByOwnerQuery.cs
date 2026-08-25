using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Application.Inventory.Results;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Queries;

public class GetInventoryByOwnerQuery
{
    public required ItemOwnerReference Owner { get; init; }
}

internal class GetInventoryByOwnerQueryHandler(
    TrpgDbContext context,
    IQueryHandler<GetInventoryItemsByOwnerQuery, IReadOnlyList<Item>> getInventoryItemsByOwner
) : IQueryHandler<GetInventoryByOwnerQuery, InventoryResult>
{
    public async Task<InventoryResult> Handle(
        GetInventoryByOwnerQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var items = await getInventoryItemsByOwner.Handle(
            new GetInventoryItemsByOwnerQuery { Owner = query.Owner },
            cancellationToken
        );

        var gold = items.OfType<Gold>().Sum(i => i.Quantity);
        var weight = items.Sum(i => i.Weight * i.Quantity);

        int? carryingCapacity = null;
        if (query.Owner.Type == OwnerType.Creature)
        {
            carryingCapacity = await context
                .Creatures.AsNoTracking()
                .Where(c => c.Id == query.Owner.Id)
                .Select(c => (int?)c.CarryingCapacity)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new InventoryResult(gold, items, weight, carryingCapacity);
    }
}
