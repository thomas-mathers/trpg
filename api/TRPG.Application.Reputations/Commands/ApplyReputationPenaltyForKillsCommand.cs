using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Configuration;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Reputations.Commands;

public record KillCrimeReport(Guid VictimId, IReadOnlyCollection<Guid> ReportedWitnessIds);

public class ApplyReputationPenaltyForKillsCommand
{
    public required Guid KillerId { get; init; }
    public required IReadOnlyCollection<KillCrimeReport> Kills { get; init; }
}

internal class ApplyReputationPenaltyForKillsCommandHandler(
    TrpgDbContext context,
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
        var victimIds = command.Kills.Select(kill => kill.VictimId).ToArray();
        var killedFactionIds = await context
            .FactionMembers.AsNoTracking()
            .Where(m => victimIds.Contains(m.CreatureId))
            .Select(m => m.FactionId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        if (killedFactionIds.Length == 0)
        {
            return;
        }

        await adjustReputations.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = command.KillerId,
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
                Adjustments = witnessAdjustments,
                TargetType = ReputationTargetType.Creature,
                Reason = ReputationReason.WitnessedKilling,
            },
            cancellationToken
        );
    }
}
