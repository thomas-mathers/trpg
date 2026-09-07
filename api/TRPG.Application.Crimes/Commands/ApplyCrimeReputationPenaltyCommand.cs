using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Configuration;
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
    public ReputationReason? VictimReason { get; init; }
}

internal class ApplyCrimeReputationPenaltyCommandHandler(
    ICommandHandler<AdjustReputationsCommand> adjustReputations,
    IOptionsMonitor<ReputationOptions> reputationOptions
) : ICommandHandler<ApplyCrimeReputationPenaltyCommand>
{
    public async Task Handle(
        ApplyCrimeReputationPenaltyCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var options = reputationOptions.CurrentValue;

        await ApplyFactionPenalties(command, cancellationToken);
        await ApplyWitnessPenalties(command, options, cancellationToken);
        await ApplyVictimPenalties(command, options, cancellationToken);
    }

    private static int Scale(int penalty, double multiplier) =>
        (int)Math.Round(penalty * multiplier, MidpointRounding.AwayFromZero);

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

    // The injured party is priced as a victim below, never also as a bystander here.
    private async Task ApplyWitnessPenalties(
        ApplyCrimeReputationPenaltyCommand command,
        ReputationOptions options,
        CancellationToken cancellationToken
    )
    {
        var adjustments = command
            .Reports.SelectMany(report =>
                report
                    .ReportedWitnessIds.Where(witnessId => witnessId != report.VictimId)
                    .Select(witnessId => new ReputationAdjustment(
                        witnessId,
                        Scale(report.Penalty, options.WitnessPenaltyMultiplier)
                    ))
            )
            .ToArray();

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

    // A reported crime reaches its victim whether or not they were there to see it.
    private async Task ApplyVictimPenalties(
        ApplyCrimeReputationPenaltyCommand command,
        ReputationOptions options,
        CancellationToken cancellationToken
    )
    {
        if (command.VictimReason is not { } victimReason)
        {
            return;
        }

        var adjustments = command
            .Reports.Where(report => report.VictimId != null)
            .Select(report => new ReputationAdjustment(
                report.VictimId!.Value,
                Scale(report.Penalty, options.VictimPenaltyMultiplier)
            ))
            .ToArray();

        await adjustReputations.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = command.PlayerId,
                WorldId = command.WorldId,
                Adjustments = adjustments,
                TargetType = ReputationTargetType.Creature,
                Reason = victimReason,
            },
            cancellationToken
        );
    }
}
