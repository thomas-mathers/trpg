using TRPG.Application.Common.Handling;
using TRPG.Application.Inventory.Results;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Queries;

public class GetInventoryByOwnerQuery
{
    public required ItemOwnerReference Owner { get; init; }
}

internal class GetInventoryByOwnerQueryHandler(
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

        return new InventoryResult(gold, items);
    }
}
