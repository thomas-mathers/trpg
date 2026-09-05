using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Factions.Queries;
using TRPG.Application.Reputations.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public class ApplyReputationPenaltyForKillsCommand
{
    public required Guid KillerId { get; init; }
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<KillCrimeReport> Kills { get; init; }
}

internal class ApplyReputationPenaltyForKillsCommandHandler(
    ICommandHandler<AdjustReputationsCommand> adjustReputations,
    IOptionsMonitor<ReputationOptions> reputationOptions
) : ICommandHandler<ApplyReputationPenaltyForKillsCommand>
{
    public async Task Handle(
        ApplyReputationPenaltyForKillsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.Kills.Count == 0)
        {
            return;
        }

        var penalty = reputationOptions.CurrentValue.KillReputationPenalty;

        await ApplyFactionPenalty(command, penalty, cancellationToken);
        await ApplyWitnessPenalty(command, penalty, cancellationToken);
    }

    private async Task ApplyFactionPenalty(
        ApplyReputationPenaltyForKillsCommand command,
        int penalty,
        CancellationToken cancellationToken
    )
    {
        var killedFactionIds = command
            .Kills.SelectMany(kill => kill.VictimFactionIds)
            .Distinct()
            .ToArray();

        if (killedFactionIds.Length == 0)
        {
            return;
        }

        await adjustReputations.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = command.KillerId,
                WorldId = command.WorldId,
                Adjustments = killedFactionIds
                    .Select(factionId => new ReputationAdjustment(factionId, penalty))
                    .ToArray(),
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.KilledFactionMember,
            },
            cancellationToken
        );
    }

    private async Task ApplyWitnessPenalty(
        ApplyReputationPenaltyForKillsCommand command,
        int penalty,
        CancellationToken cancellationToken
    )
    {
        var witnessAdjustments = command
            .Kills.SelectMany(kill =>
                kill.ReportedWitnessIds.Select(witnessId => new ReputationAdjustment(
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
                CreatureId = command.KillerId,
                WorldId = command.WorldId,
                Adjustments = witnessAdjustments,
                TargetType = ReputationTargetType.Creature,
                Reason = ReputationReason.WitnessedKilling,
            },
            cancellationToken
        );
    }
}
