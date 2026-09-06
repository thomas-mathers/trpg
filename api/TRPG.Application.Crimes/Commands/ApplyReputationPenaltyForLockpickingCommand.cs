using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Crimes.Queries;
using TRPG.Application.Reputations.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public class ApplyReputationPenaltyForLockpickingCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<LockpickingCrimeReport> Crimes { get; init; }
}

internal class ApplyReputationPenaltyForLockpickingCommandHandler(
    IQueryHandler<
        GetLockpickingCrimeDetailsByIdsQuery,
        IReadOnlyDictionary<Guid, LockpickingCrimeDetails>
    > getLockpickingCrimeDetailsByIds,
    ICommandHandler<AdjustReputationsCommand> adjustReputations,
    IOptionsMonitor<ReputationOptions> reputationOptions
) : ICommandHandler<ApplyReputationPenaltyForLockpickingCommand>
{
    public async Task Handle(
        ApplyReputationPenaltyForLockpickingCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.Crimes.Count == 0)
        {
            return;
        }

        var crimeIds = command.Crimes.Select(crime => crime.CrimeId).ToArray();
        var crimeDetails = await getLockpickingCrimeDetailsByIds.Handle(
            new GetLockpickingCrimeDetailsByIdsQuery { CrimeIds = crimeIds },
            cancellationToken
        );

        await ApplyFactionPenalty(command, crimeDetails, cancellationToken);
        await ApplyWitnessPenalty(command, crimeDetails, cancellationToken);
    }

    private async Task ApplyFactionPenalty(
        ApplyReputationPenaltyForLockpickingCommand command,
        IReadOnlyDictionary<Guid, LockpickingCrimeDetails> crimeDetails,
        CancellationToken cancellationToken
    )
    {
        var penaltyByFactionId = command
            .Crimes.Where(crime => crimeDetails[crime.CrimeId].OwnerFactionId != null)
            .GroupBy(crime => crimeDetails[crime.CrimeId].OwnerFactionId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(crime => PenaltyFor(crimeDetails[crime.CrimeId].Outcome))
            );

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
                Reason = ReputationReason.PickedFactionLock,
            },
            cancellationToken
        );
    }

    private async Task ApplyWitnessPenalty(
        ApplyReputationPenaltyForLockpickingCommand command,
        IReadOnlyDictionary<Guid, LockpickingCrimeDetails> crimeDetails,
        CancellationToken cancellationToken
    )
    {
        var witnessAdjustments = command
            .Crimes.SelectMany(crime =>
                crime.ReportedWitnessIds.Select(witnessId => new ReputationAdjustment(
                    witnessId,
                    PenaltyFor(crimeDetails[crime.CrimeId].Outcome)
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
                Reason = ReputationReason.WitnessedLockpicking,
            },
            cancellationToken
        );
    }

    private int PenaltyFor(LockpickingCrimeOutcome? outcome) =>
        outcome == LockpickingCrimeOutcome.SettledWithGuard
            ? reputationOptions.CurrentValue.SettledLockpickingReputationPenalty
            : reputationOptions.CurrentValue.LockpickingReputationPenalty;
}
