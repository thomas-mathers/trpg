using Microsoft.Extensions.Options;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Reputations.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Queries;

public class GetGuardEncounterStateQuery
{
    public required Guid EncounterId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid GuardCreatureId { get; init; }
    public required Guid LocationId { get; init; }
}

internal class GetGuardEncounterStateQueryHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<GetCityFactionForCreatureQuery, Guid?> getCityFactionForCreature,
    IQueryHandler<GetReputationScoreQuery, int> getReputationScore,
    IQueryHandler<
        GetRecentReputationLogQuery,
        IReadOnlyCollection<ReputationLogEntry>
    > getRecentReputationLog,
    IOptionsMonitor<GuardEncounterOptions> guardEncounterOptions
) : IQueryHandler<GetGuardEncounterStateQuery, GuardEncounterState>
{
    private const int RecentOffenseLimit = 3;

    public async Task<GuardEncounterState> Handle(
        GetGuardEncounterStateQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var guard = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = query.GuardCreatureId },
            cancellationToken
        );
        var location = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = query.LocationId },
            cancellationToken
        );
        var cityFactionId = await getCityFactionForCreature.Handle(
            new GetCityFactionForCreatureQuery { CreatureId = query.GuardCreatureId },
            cancellationToken
        );

        if (cityFactionId == null)
        {
            throw new InvalidOperationException(
                $"Guard {query.GuardCreatureId} has no city faction membership."
            );
        }

        var options = guardEncounterOptions.CurrentValue;
        var score = await getReputationScore.Handle(
            new GetReputationScoreQuery
            {
                CreatureId = query.PlayerId,
                TargetId = cityFactionId.Value,
                TargetType = ReputationTargetType.Faction,
            },
            cancellationToken
        );

        var recentOffenses = await getRecentReputationLog.Handle(
            new GetRecentReputationLogQuery
            {
                CreatureId = query.PlayerId,
                TargetId = cityFactionId.Value,
                TargetType = ReputationTargetType.Faction,
                Limit = RecentOffenseLimit,
                NegativeOnly = true,
            },
            cancellationToken
        );

        return new GuardEncounterState(
            query.EncounterId,
            guard!.Name,
            location!.Name,
            GuardEncounterCalculator.ComputeFineGold(score, options),
            GuardEncounterCalculator.ComputeJailHours(score, options),
            recentOffenses.Select(o => o.Reason).ToArray()
        );
    }
}
