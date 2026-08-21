using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Configuration;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Reputations.Commands;

public class ApplyReputationPenaltyForTheftsCommand
{
    public required Guid PlayerId { get; init; }
    public required IReadOnlyCollection<Guid> TheftCrimeIds { get; init; }
}

internal class ApplyReputationPenaltyForTheftsCommandHandler(
    TrpgDbContext context,
    ICommandHandler<AdjustReputationCommand> adjustReputation,
    IOptionsMonitor<ReputationOptions> reputationOptions
) : ICommandHandler<ApplyReputationPenaltyForTheftsCommand>
{
    public async Task Handle(
        ApplyReputationPenaltyForTheftsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.TheftCrimeIds.Count == 0)
        {
            return;
        }

        var thefts = await context
            .Crimes.OfType<TheftCrime>()
            .Where(crime => command.TheftCrimeIds.AsEnumerable().Contains(crime.Id))
            .Select(crime => new { crime.OwnerFactionId, crime.Outcome })
            .ToArrayAsync(cancellationToken);

        var penaltyByFactionId = thefts
            .Where(theft => theft.OwnerFactionId != null)
            .GroupBy(theft => theft.OwnerFactionId!.Value)
            .ToDictionary(
                group => group.Key,
                group =>
                    group.Sum(theft =>
                        theft.Outcome == TheftCrimeOutcome.Apologized
                            ? reputationOptions.CurrentValue.ApologizedTheftReputationPenalty
                            : reputationOptions.CurrentValue.TheftReputationPenalty
                    )
            );

        foreach (var (factionId, penalty) in penaltyByFactionId)
        {
            await adjustReputation.Handle(
                new AdjustReputationCommand
                {
                    CreatureId = command.PlayerId,
                    TargetIds = [factionId],
                    TargetType = ReputationTargetType.Faction,
                    DeltaScore = penalty,
                    Reason = ReputationReason.StoleFromFactionMember,
                    Detail = "Witnessed theft",
                },
                cancellationToken
            );
        }
    }
}
