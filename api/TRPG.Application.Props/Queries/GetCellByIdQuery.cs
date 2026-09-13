using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Queries;

public class GetCellByIdQuery
{
    public required Guid Id { get; init; }
}

internal class GetCellByIdQueryHandler(IPropsDbContext context)
    : IQueryHandler<GetCellByIdQuery, Cell?>
{
    public async Task<Cell?> Handle(
        GetCellByIdQuery query,
        CancellationToken cancellationToken = default
    ) =>
        await context
            .Props.OfType<Cell>()
            .AsNoTracking()
            .FirstOrDefaultAsync(cell => cell.Id == query.Id, cancellationToken);
}
