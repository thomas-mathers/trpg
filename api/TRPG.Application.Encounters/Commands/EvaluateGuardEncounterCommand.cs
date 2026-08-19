using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Queries;
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
    IQueryHandler<GetActiveEncounterQuery, HostileEncounter?> getActiveEncounter,
    IQueryHandler<GetActiveGuardEncounterQuery, GuardEncounter?> getActiveGuardEncounter,
    IQueryHandler<GetActiveFightQuery, Fight?> getActiveFight,
    IQueryHandler<GetCityFactionForCreatureQuery, Guid?> getCityFactionForCreature,
    IQueryHandler<GetReputationScoreQuery, int> getReputationScore,
    IQueryHandler<
        GetRecentReputationLogQuery,
        IReadOnlyCollection<ReputationLogEntry>
    > getRecentReputationLog,
    IOptionsMonitor<GuardEncounterOptions> guardEncounterOptions
) : ICommandHandler<EvaluateGuardEncounterCommand, GuardEncounterState?>
{
    private const int RecentOffenseLimit = 3;

    public async Task<GuardEncounterState?> Handle(
        EvaluateGuardEncounterCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var activeEncounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = command.PlayerId },
            cancellationToken
        );
        if (activeEncounter != null)
        {
            return null;
        }

        var activeGuardEncounter = await getActiveGuardEncounter.Handle(
            new GetActiveGuardEncounterQuery { PlayerId = command.PlayerId },
            cancellationToken
        );
        if (activeGuardEncounter != null)
        {
            return null;
        }

        var activeFight = await getActiveFight.Handle(
            new GetActiveFightQuery { PlayerId = command.PlayerId },
            cancellationToken
        );
        if (activeFight != null)
        {
            return null;
        }

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
            return null;
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

        var location = await context
            .Locations.AsNoTracking()
            .FirstAsync(l => l.Id == player!.LocationId, cancellationToken);

        var encounter = new GuardEncounter
        {
            WorldId = command.WorldId,
            PlayerId = command.PlayerId,
            LocationId = player!.LocationId,
            GuardCreatureId = guard.Id,
        };
        context.Encounters.Add(encounter);
        await context.SaveChangesAsync(cancellationToken);

        var recentOffenses = await getRecentReputationLog.Handle(
            new GetRecentReputationLogQuery
            {
                CreatureId = command.PlayerId,
                TargetId = cityFactionId.Value,
                TargetType = ReputationTargetType.Faction,
                Limit = RecentOffenseLimit,
                NegativeOnly = true,
            },
            cancellationToken
        );

        return new GuardEncounterState(
            encounter.Id,
            guard.Name,
            location.Name,
            GuardEncounterCalculator.ComputeFineGold(score, options),
            GuardEncounterCalculator.ComputeJailHours(score, options),
            recentOffenses.Select(o => o.Reason).ToArray()
        );
    }
}
