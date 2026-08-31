using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Inventory.Queries;

public class ValidateTransferItemsQuery
{
    public required ItemOwnerReference From { get; init; }
    public required IReadOnlyList<ItemSelection> Selections { get; init; }
}

internal class ValidateTransferItemsQueryHandler(TrpgDbContext context)
    : IQueryHandler<ValidateTransferItemsQuery, IReadOnlyCollection<TransferItem>>
{
    public Task<IReadOnlyCollection<TransferItem>> Handle(
        ValidateTransferItemsQuery query,
        CancellationToken cancellationToken = default
    ) =>
        InventoryTransferValidation.GetValidatedTransferItems(
            query.From,
            query.Selections,
            context.Items.AsNoTracking(),
            cancellationToken
        );
}
