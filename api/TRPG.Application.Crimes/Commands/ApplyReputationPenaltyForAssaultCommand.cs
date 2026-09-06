using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Factions.Queries;
using TRPG.Application.Reputations.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public class ApplyReputationPenaltyForAssaultCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<AssaultCrimeReport> Assaults { get; init; }
}

internal class ApplyReputationPenaltyForAssaultCommandHandler(
    ICommandHandler<AdjustReputationsCommand> adjustReputations,
    IOptionsMonitor<ReputationOptions> reputationOptions
) : ICommandHandler<ApplyReputationPenaltyForAssaultCommand>
{
    public async Task Handle(
        ApplyReputationPenaltyForAssaultCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.Assaults.Count == 0)
        {
            return;
        }

        var penalty = reputationOptions.CurrentValue.AssaultReputationPenalty;

        await ApplyFactionPenalty(command, penalty, cancellationToken);
        await ApplyWitnessPenalty(command, penalty, cancellationToken);
    }

    private async Task ApplyFactionPenalty(
        ApplyReputationPenaltyForAssaultCommand command,
        int penalty,
        CancellationToken cancellationToken
    )
    {
        var assaultedFactionIds = command
            .Assaults.SelectMany(assault => assault.VictimFactionIds)
            .Distinct()
            .ToArray();

        if (assaultedFactionIds.Length == 0)
        {
            return;
        }

        await adjustReputations.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = command.PlayerId,
                WorldId = command.WorldId,
                Adjustments = assaultedFactionIds
                    .Select(factionId => new ReputationAdjustment(factionId, penalty))
                    .ToArray(),
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.AssaultedFactionMember,
            },
            cancellationToken
        );
    }

    private async Task ApplyWitnessPenalty(
        ApplyReputationPenaltyForAssaultCommand command,
        int penalty,
        CancellationToken cancellationToken
    )
    {
        var witnessAdjustments = command
            .Assaults.SelectMany(assault =>
                assault.ReportedWitnessIds.Select(witnessId => new ReputationAdjustment(
                    witnessId,
                    penalty
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
                WorldId = command.WorldId,
                Adjustments = witnessAdjustments,
                TargetType = ReputationTargetType.Creature,
                Reason = ReputationReason.WitnessedAssault,
            },
            cancellationToken
        );
    }
}
