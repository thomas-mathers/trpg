using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Factions.Queries;
using TRPG.Application.Locations.Queries;
using TRPG.Application.Reputations.Mappers;
using TRPG.Application.Reputations.Queries;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class EvaluateGuardEncounterCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
}

internal class EvaluateGuardEncounterCommandHandler(
    TrpgDbContext context,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetCityFactionForCreatureQuery, Guid?> getCityFactionForCreature,
    IQueryHandler<GetReputationScoreQuery, int> getReputationScore,
    IQueryHandler<
        GetRecentReputationLogQuery,
        IReadOnlyCollection<ReputationLogEntry>
    > getRecentReputationLog,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IOptionsMonitor<GuardEncounterOptions> guardEncounterOptions
) : ICommandHandler<EvaluateGuardEncounterCommand, GuardEncounter?>
{
    private const int RecentOffenseLimit = 3;

    public async Task<GuardEncounter?> Handle(
        EvaluateGuardEncounterCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );

        var guard = await context
            .Creatures.AsNoTracking()
            .Where(c =>
                c.WorldId == command.WorldId
                && c.LocationId == player!.LocationId
                && c.Profession == Profession.Guard
                && c.State != CreatureState.Dead
            )
            .FirstOrDefaultAsync(cancellationToken);
        if (guard == null)
        {
            return null;
        }

        var cityFactionId = await getCityFactionForCreature.Handle(
            new GetCityFactionForCreatureQuery { CreatureId = guard.Id },
            cancellationToken
        );
        if (cityFactionId == null)
        {
            throw new InvalidOperationException(
                $"Guard {guard.Id} has no city faction membership."
            );
        }

        var options = guardEncounterOptions.CurrentValue;
        var score = await getReputationScore.Handle(
            new GetReputationScoreQuery
            {
                CreatureId = command.PlayerId,
                TargetId = cityFactionId.Value,
                TargetType = ReputationTargetType.Faction,
            },
            cancellationToken
        );
        if (score > options.ReputationThreshold)
        {
            return null;
        }

        if (Random.Shared.NextDouble() >= options.EncounterChance)
        {
            return null;
        }

        var location =
            await getLocationById.Handle(
                new GetLocationByIdQuery { Id = player!.LocationId },
                cancellationToken
            ) ?? throw new InvalidOperationException($"Location {player!.LocationId} not found.");

        var recentOffenses = await getRecentReputationLog.Handle(
            new GetRecentReputationLogQuery
            {
                CreatureId = command.PlayerId,
                Targets =
                [
                    new ReputationLogTarget(cityFactionId.Value, ReputationTargetType.Faction),
                ],
                Limit = RecentOffenseLimit,
                NegativeOnly = true,
            },
            cancellationToken
        );

        var encounter = new GuardEncounter
        {
            WorldId = command.WorldId,
            PlayerId = command.PlayerId,
            LocationId = player!.LocationId,
            GuardCreatureId = guard.Id,
            CityFactionId = cityFactionId.Value,
            GuardName = guard.Name,
            LocationName = location.Name,
            ReputationScore = score,
            FineAmount = GuardEncounterCalculator.ComputeFineGold(score, options),
            JailHours = GuardEncounterCalculator.ComputeJailHours(score, options),
            RecentOffenses = recentOffenses
                .Select(entry => entry.Detail ?? entry.Reason.ToDisplayText())
                .ToList(),
        };
        context.Encounters.Add(encounter);
        await context.SaveChangesAsync(cancellationToken);

        return encounter;
    }
}
