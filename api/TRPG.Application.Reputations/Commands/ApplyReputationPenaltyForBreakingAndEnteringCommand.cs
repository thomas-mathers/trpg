using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Crimes.Commands;
using TRPG.Application.Crimes.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Reputations.Commands;

public class ApplyReputationPenaltyForBreakingAndEnteringCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<BreakingAndEnteringCrimeReport> Crimes { get; init; }
}

internal class ApplyReputationPenaltyForBreakingAndEnteringCommandHandler(
    IQueryHandler<
        GetBreakingAndEnteringCrimeDetailsByIdsQuery,
        IReadOnlyDictionary<Guid, BreakingAndEnteringCrimeDetails>
    > getBreakingAndEnteringCrimeDetailsByIds,
    ICommandHandler<AdjustReputationsCommand> adjustReputations,
    IOptionsMonitor<ReputationOptions> reputationOptions
) : ICommandHandler<ApplyReputationPenaltyForBreakingAndEnteringCommand>
{
    public async Task Handle(
        ApplyReputationPenaltyForBreakingAndEnteringCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.Crimes.Count == 0)
        {
            return;
        }

        var crimeIds = command.Crimes.Select(crime => crime.CrimeId).ToArray();
        var crimeDetails = await getBreakingAndEnteringCrimeDetailsByIds.Handle(
            new GetBreakingAndEnteringCrimeDetailsByIdsQuery { CrimeIds = crimeIds },
            cancellationToken
        );

        var penalty = reputationOptions.CurrentValue.BreakingAndEnteringReputationPenalty;

        await ApplyFactionPenalty(command, crimeDetails, penalty, cancellationToken);
        await ApplyWitnessPenalty(command, penalty, cancellationToken);
    }

    private async Task ApplyFactionPenalty(
        ApplyReputationPenaltyForBreakingAndEnteringCommand command,
        IReadOnlyDictionary<Guid, BreakingAndEnteringCrimeDetails> crimeDetails,
        int penalty,
        CancellationToken cancellationToken
    )
    {
        var penaltyByFactionId = command
            .Crimes.Where(crime => crimeDetails[crime.CrimeId].OwnerFactionId != null)
            .GroupBy(crime => crimeDetails[crime.CrimeId].OwnerFactionId!.Value)
            .ToDictionary(group => group.Key, _ => penalty);

        if (penaltyByFactionId.Count == 0)
        {
            return;
        }

        await adjustReputations.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = command.PlayerId,
                WorldId = command.WorldId,
                Adjustments = penaltyByFactionId
                    .Select(pair => new ReputationAdjustment(pair.Key, pair.Value))
                    .ToArray(),
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.BrokeIntoFactionProperty,
            },
            cancellationToken
        );
    }

    private async Task ApplyWitnessPenalty(
        ApplyReputationPenaltyForBreakingAndEnteringCommand command,
        int penalty,
        CancellationToken cancellationToken
    )
    {
        var witnessAdjustments = command
            .Crimes.SelectMany(crime =>
                crime.ReportedWitnessIds.Select(witnessId => new ReputationAdjustment(
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
                Reason = ReputationReason.WitnessedBreakingAndEntering,
            },
            cancellationToken
        );
    }
}
