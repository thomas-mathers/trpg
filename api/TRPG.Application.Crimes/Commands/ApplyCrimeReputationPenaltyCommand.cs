using TRPG.Application.Common.Commands;
using TRPG.Application.Reputations.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public class ApplyCrimeReputationPenaltyCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<CrimeReport> Reports { get; init; }
    public required ReputationReason FactionReason { get; init; }
    public required ReputationReason WitnessReason { get; init; }
}

internal class ApplyCrimeReputationPenaltyCommandHandler(
    ICommandHandler<AdjustReputationsCommand> adjustReputations
) : ICommandHandler<ApplyCrimeReputationPenaltyCommand>
{
    public async Task Handle(
        ApplyCrimeReputationPenaltyCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await ApplyFactionPenalties(command, cancellationToken);
        await ApplyWitnessPenalties(command, cancellationToken);
    }

    // Each offence counts, so repeated crimes against one faction accumulate.
    private async Task ApplyFactionPenalties(
        ApplyCrimeReputationPenaltyCommand command,
        CancellationToken cancellationToken
    )
    {
        var adjustments = command
            .Reports.SelectMany(report =>
                report.FactionIds.Select(factionId => (FactionId: factionId, report.Penalty))
            )
            .GroupBy(entry => entry.FactionId)
            .Select(group => new ReputationAdjustment(group.Key, group.Sum(entry => entry.Penalty)))
            .ToArray();

        if (adjustments.Length == 0)
        {
            return;
        }

        await adjustReputations.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = command.PlayerId,
                WorldId = command.WorldId,
                Adjustments = adjustments,
                TargetType = ReputationTargetType.Faction,
                Reason = command.FactionReason,
            },
            cancellationToken
        );
    }

    private async Task ApplyWitnessPenalties(
        ApplyCrimeReputationPenaltyCommand command,
        CancellationToken cancellationToken
    )
    {
        var adjustments = command
            .Reports.SelectMany(report =>
                report.ReportedWitnessIds.Select(witnessId => new ReputationAdjustment(
                    witnessId,
                    report.Penalty
                ))
            )
            .ToArray();

        if (adjustments.Length == 0)
        {
            return;
        }

        await adjustReputations.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = command.PlayerId,
                WorldId = command.WorldId,
                Adjustments = adjustments,
                TargetType = ReputationTargetType.Creature,
                Reason = command.WitnessReason,
            },
            cancellationToken
        );
    }
}
