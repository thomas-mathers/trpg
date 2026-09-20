using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Props.Queries;

public class GetActivatedTriggerIdsQuery
{
    public required Guid WorldId { get; init; }
}

internal class GetActivatedTriggerIdsQueryHandler(IPropsDbContext context)
    : IQueryHandler<GetActivatedTriggerIdsQuery, IReadOnlySet<Guid>>
{
    public async Task<IReadOnlySet<Guid>> Handle(
        GetActivatedTriggerIdsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        return await context
            .Props.AsNoTracking()
            .OfType<Trigger>()
            .Where(trigger => trigger.WorldId == query.WorldId && trigger.IsActivated)
            .Select(trigger => trigger.Id)
            .ToHashSetAsync(cancellationToken);
    }
}
