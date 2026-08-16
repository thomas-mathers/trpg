using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;
using TRPG.Data;

namespace TRPG.Application.Creatures.Queries;

public class GetUnallocatedAttributePointsQuery
{
    public required Guid CreatureId { get; init; }
}

internal class GetUnallocatedAttributePointsQueryHandler(
    TrpgDbContext context,
    IOptionsSnapshot<CreatureGeneratorOptions> optionsSnapshot
) : IQueryHandler<GetUnallocatedAttributePointsQuery, int>
{
    public async Task<int> Handle(
        GetUnallocatedAttributePointsQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var creature = await context
            .Creatures.AsNoTracking()
            .FirstAsync(c => c.Id == query.CreatureId, cancellationToken);

        return StatFormulas.CalculateUnallocatedAttributePoints(
            creature.BaseAttributes,
            creature.Level,
            optionsSnapshot.Value
        );
    }
}
