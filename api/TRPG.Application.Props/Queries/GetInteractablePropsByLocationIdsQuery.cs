using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Queries;

public class GetInteractablePropsByLocationIdsQuery
{
    public required IReadOnlyCollection<Guid> LocationIds { get; init; }
}

public record InteractableProp(
    Guid Id,
    Guid LocationId,
    string Name,
    bool? IsPulled,
    bool IsLocked
);

internal class GetInteractablePropsByLocationIdsQueryHandler(IPropsDbContext context)
    : IQueryHandler<GetInteractablePropsByLocationIdsQuery, IReadOnlyList<InteractableProp>>
{
    public async Task<IReadOnlyList<InteractableProp>> Handle(
        GetInteractablePropsByLocationIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var props = await context
            .Props.AsNoTracking()
            .Where(prop =>
                query.LocationIds.AsEnumerable().Contains(prop.LocationId)
                && (prop is Container || prop is Lever || prop is Cell)
            )
            .ToArrayAsync(cancellationToken);

        return props
            .Select(prop => new InteractableProp(
                prop.Id,
                prop.LocationId,
                prop.Name,
                prop is Lever lever ? lever.IsPulled : null,
                prop switch
                {
                    Container container => container.KeyItemId != null,
                    Cell cell => cell.IsLocked,
                    _ => false,
                }
            ))
            .ToArray();
    }
}
