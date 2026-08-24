using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Configuration;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Reputations.Commands;

public record TheftCrimeReport(Guid TheftCrimeId, IReadOnlyCollection<Guid> ReportedWitnessIds);

public class ApplyReputationPenaltyForTheftsCommand
{
    public required Guid PlayerId { get; init; }
    public required IReadOnlyCollection<TheftCrimeReport> Thefts { get; init; }
}

internal record TheftCrimeDetails(Guid Id, Guid? OwnerFactionId, TheftCrimeOutcome? Outcome);

internal class ApplyReputationPenaltyForTheftsCommandHandler(
    TrpgDbContext context,
    ICommandHandler<AdjustReputationsCommand> adjustReputations,
    IOptionsMonitor<ReputationOptions> reputationOptions
) : ICommandHandler<ApplyReputationPenaltyForTheftsCommand>
{
    public async Task Handle(
        ApplyReputationPenaltyForTheftsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.Thefts.Count == 0)
        {
            return;
        }

        var theftCrimeIds = command.Thefts.Select(theft => theft.TheftCrimeId).ToArray();
        var thefts = await context
            .Crimes.OfType<TheftCrime>()
            .Where(crime => theftCrimeIds.Contains(crime.Id))
            .Select(crime => new TheftCrimeDetails(crime.Id, crime.OwnerFactionId, crime.Outcome))
            .ToDictionaryAsync(theft => theft.Id, cancellationToken);

        await ApplyFactionPenalty(command, thefts, cancellationToken);
        await ApplyWitnessPenalty(command, thefts, cancellationToken);
    }

    private async Task ApplyFactionPenalty(
        ApplyReputationPenaltyForTheftsCommand command,
        Dictionary<Guid, TheftCrimeDetails> thefts,
        CancellationToken cancellationToken
    )
    {
        var penaltyByFactionId = command
            .Thefts.Where(theft => thefts[theft.TheftCrimeId].OwnerFactionId != null)
            .GroupBy(theft => thefts[theft.TheftCrimeId].OwnerFactionId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(theft => PenaltyFor(thefts[theft.TheftCrimeId].Outcome))
            );

        if (penaltyByFactionId.Count == 0)
        {
            return;
        }

        await adjustReputations.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = command.PlayerId,
                Adjustments = penaltyByFactionId
                    .Select(pair => new ReputationAdjustment(pair.Key, pair.Value))
                    .ToArray(),
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.StoleFromFactionMember,
            },
            cancellationToken
        );
    }

    private async Task ApplyWitnessPenalty(
        ApplyReputationPenaltyForTheftsCommand command,
        Dictionary<Guid, TheftCrimeDetails> thefts,
        CancellationToken cancellationToken
    )
    {
        var witnessAdjustments = command
            .Thefts.SelectMany(theft =>
                theft.ReportedWitnessIds.Select(witnessId => new ReputationAdjustment(
                    witnessId,
                    PenaltyFor(thefts[theft.TheftCrimeId].Outcome)
                ))
            )
            .ToArray();

        if (witnessAdjustments.Length == 0)
        {
            return;
        }

        await adjustReputations.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = command.PlayerId,
                Adjustments = witnessAdjustments,
                TargetType = ReputationTargetType.Creature,
                Reason = ReputationReason.WitnessedTheft,
            },
            cancellationToken
        );
    }

    private int PenaltyFor(TheftCrimeOutcome? outcome) =>
        outcome == TheftCrimeOutcome.Apologized
            ? reputationOptions.CurrentValue.ApologizedTheftReputationPenalty
            : reputationOptions.CurrentValue.TheftReputationPenalty;
}
