using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Crimes.Queries;
using TRPG.Application.Reputations.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public class ApplyReputationPenaltyForTrespassingCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<TrespassingCrimeReport> Crimes { get; init; }
}

internal class ApplyReputationPenaltyForTrespassingCommandHandler(
    IQueryHandler<
        GetTrespassingCrimeDetailsByIdsQuery,
        IReadOnlyDictionary<Guid, TrespassingCrimeDetails>
    > getTrespassingCrimeDetailsByIds,
    ICommandHandler<AdjustReputationsCommand> adjustReputations,
    IOptionsMonitor<ReputationOptions> reputationOptions
) : ICommandHandler<ApplyReputationPenaltyForTrespassingCommand>
{
    public async Task Handle(
        ApplyReputationPenaltyForTrespassingCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.Crimes.Count == 0)
        {
            return;
        }

        var crimeIds = command.Crimes.Select(crime => crime.CrimeId).ToArray();
        var crimeDetails = await getTrespassingCrimeDetailsByIds.Handle(
            new GetTrespassingCrimeDetailsByIdsQuery { CrimeIds = crimeIds },
            cancellationToken
        );

        var penalty = reputationOptions.CurrentValue.TrespassingReputationPenalty;

        await ApplyFactionPenalty(command, crimeDetails, penalty, cancellationToken);
        await ApplyWitnessPenalty(command, penalty, cancellationToken);
    }

    private async Task ApplyFactionPenalty(
        ApplyReputationPenaltyForTrespassingCommand command,
        IReadOnlyDictionary<Guid, TrespassingCrimeDetails> crimeDetails,
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
                Reason = ReputationReason.TrespassedOnFactionProperty,
            },
            cancellationToken
        );
    }

    private async Task ApplyWitnessPenalty(
        ApplyReputationPenaltyForTrespassingCommand command,
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
                Reason = ReputationReason.WitnessedTrespassing,
            },
            cancellationToken
        );
    }
}
