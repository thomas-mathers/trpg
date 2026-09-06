using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Reputations.Mappers;
using TRPG.Application.Reputations.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class CreateGuardEncounterCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid PlayerLocationId { get; init; }
    public required string LocationName { get; init; }
    public required Guid GuardCreatureId { get; init; }
    public required string GuardName { get; init; }
    public required Guid CityFactionId { get; init; }
    public required int ReputationScore { get; init; }
    public Guid? TriggeringCrimeId { get; init; }
}

internal class CreateGuardEncounterCommandHandler(
    IEncountersDbContext context,
    IQueryHandler<
        GetRecentReputationLogQuery,
        IReadOnlyCollection<ReputationLogEntry>
    > getRecentReputationLog,
    IOptionsMonitor<GuardEncounterOptions> guardEncounterOptions
) : ICommandHandler<CreateGuardEncounterCommand, GuardEncounter>
{
    private const int RecentOffenseLimit = 3;

    public async Task<GuardEncounter> Handle(
        CreateGuardEncounterCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var recentOffenses = await getRecentReputationLog.Handle(
            new GetRecentReputationLogQuery
            {
                CreatureId = command.PlayerId,
                Targets =
                [
                    new ReputationLogTarget(command.CityFactionId, ReputationTargetType.Faction),
                ],
                Limit = RecentOffenseLimit,
                NegativeOnly = true,
            },
            cancellationToken
        );

        var options = guardEncounterOptions.CurrentValue;
        var encounter = new GuardEncounter
        {
            WorldId = command.WorldId,
            PlayerId = command.PlayerId,
            LocationId = command.PlayerLocationId,
            LocationName = command.LocationName,
            GuardCreatureId = command.GuardCreatureId,
            CityFactionId = command.CityFactionId,
            GuardName = command.GuardName,
            ReputationScore = command.ReputationScore,
            FineAmount = GuardEncounterCalculator.ComputeFineGold(command.ReputationScore, options),
            JailHours = GuardEncounterCalculator.ComputeJailHours(command.ReputationScore, options),
            RecentOffenses = recentOffenses
                .Select(entry => entry.Detail ?? entry.Reason.ToDisplayText())
                .ToList(),
            TriggeringCrimeId = command.TriggeringCrimeId,
        };
        context.Encounters.Add(encounter);
        await context.SaveChangesAsync(cancellationToken);

        return encounter;
    }
}
