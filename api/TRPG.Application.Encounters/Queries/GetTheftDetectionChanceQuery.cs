using Microsoft.Extensions.Options;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures;
using TRPG.Application.Inventory;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Queries;

public class GetTheftDetectionChanceQuery
{
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required ItemOwnerReference From { get; init; }
    public required IReadOnlyList<ItemSelection> Items { get; init; }
}

internal class GetTheftDetectionChanceQueryHandler(
    TheftSourceResolver theftSourceResolver,
    SkillCheckService skillCheckService,
    IOptionsMonitor<TheftOptions> theftOptions
) : IQueryHandler<GetTheftDetectionChanceQuery, float?>
{
    public async Task<float?> Handle(
        GetTheftDetectionChanceQuery query,
        CancellationToken cancellationToken = default
    )
    {
        if (query.Items.Count == 0)
        {
            return null;
        }

        var source = await theftSourceResolver.Resolve(
            query.From,
            query.WorldId,
            cancellationToken
        );
        if (source == null)
        {
            return null;
        }

        var witnesses = await theftSourceResolver.GetLiveWitnesses(
            query.WorldId,
            source.LocationId,
            query.PlayerId,
            cancellationToken
        );
        var requiresTheftDetectionRoll = source.IsPickpocketing || witnesses.Length > 0;
        if (!requiresTheftDetectionRoll)
        {
            return 1f;
        }

        var totalQuantity = query.Items.Sum(item => item.Quantity);
        var curve = TheftDetectionChanceCalculator.BuildCurve(
            theftOptions.CurrentValue,
            totalQuantity
        );
        var detectionChance = await skillCheckService.CalculateChance(
            query.PlayerId,
            source.Skill,
            curve,
            cancellationToken
        );

        return 1f - detectionChance;
    }
}
