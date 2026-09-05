using System.Transactions;
using Microsoft.Extensions.Options;
using TRPG.Application.Combat;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Reputations.Commands;
using TRPG.Application.Reputations.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class ResolveSuspicionEncounterActionCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required SuspicionEncounterAction Action { get; init; }
    public required Guid EncounterId { get; init; }
    public required Guid GuardCreatureId { get; init; }
    public required string GuardName { get; init; }
    public required Guid CityFactionId { get; init; }
    public required Guid EncounterLocationId { get; init; }
    public required string LocationName { get; init; }
}

internal class ResolveSuspicionEncounterActionCommandHandler(
    ICommandHandler<CompleteEncounterCommand> completeEncounter,
    ICommandHandler<AdjustReputationsCommand> adjustReputations,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetReputationScoreQuery, int> getReputationScore,
    ICommandHandler<CreateGuardEncounterCommand, GuardEncounter> createGuardEncounter,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted,
    IOptionsSnapshot<FleeOptions> fleeOptions,
    IOptionsMonitor<SuspicionOptions> suspicionOptions
) : ICommandHandler<ResolveSuspicionEncounterActionCommand, SuspicionEncounterResolutionFact>
{
    public async Task<SuspicionEncounterResolutionFact> Handle(
        ResolveSuspicionEncounterActionCommand command,
        CancellationToken cancellationToken = default
    )
    {
        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var fact = command.Action switch
        {
            ComplySuspicionAction => await ResolveComply(command, cancellationToken),
            FleeSuspicionAction => await ResolveFlee(command, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };

        await completeEncounter.Handle(
            new CompleteEncounterCommand { EncounterId = command.EncounterId },
            cancellationToken
        );

        transaction.Complete();

        return fact;
    }

    private async Task<SuspicionEncounterResolutionFact> ResolveComply(
        ResolveSuspicionEncounterActionCommand command,
        CancellationToken cancellationToken
    )
    {
        var options = suspicionOptions.CurrentValue;
        await adjustReputations.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = command.PlayerId,
                WorldId = command.WorldId,
                Adjustments =
                [
                    new ReputationAdjustment(
                        command.CityFactionId,
                        -options.ComplyReputationPenalty
                    ),
                ],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.CaughtSneaking,
                Detail = $"Caught sneaking near {command.GuardName}",
            },
            cancellationToken
        );

        return new SuspicionEncounterResolutionFact(
            command.EncounterId,
            SuspicionEncounterResolutionOutcome.Complied,
            command.GuardName,
            command.LocationName,
            null
        );
    }

    private async Task<SuspicionEncounterResolutionFact> ResolveFlee(
        ResolveSuspicionEncounterActionCommand command,
        CancellationToken cancellationToken
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );
        var guard = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.GuardCreatureId },
            cancellationToken
        );

        var catchChance = EvadeChanceCalculator.CatchChance(
            fleeOptions.Value,
            ToEvadeParticipant(player!),
            [ToEvadeParticipant(guard!)]
        );
        var isCaught = Random.Shared.NextDouble() < catchChance;

        if (!isCaught)
        {
            return new SuspicionEncounterResolutionFact(
                command.EncounterId,
                SuspicionEncounterResolutionOutcome.Fled,
                command.GuardName,
                command.LocationName,
                null
            );
        }

        var options = suspicionOptions.CurrentValue;
        await adjustReputations.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = command.PlayerId,
                WorldId = command.WorldId,
                Adjustments =
                [
                    new ReputationAdjustment(
                        command.CityFactionId,
                        -options.FleeFailedReputationPenalty
                    ),
                ],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.CaughtFleeingSuspicion,
                Detail = $"Fled from {command.GuardName}'s questioning and was caught",
            },
            cancellationToken
        );

        var newReputationScore = await getReputationScore.Handle(
            new GetReputationScoreQuery
            {
                CreatureId = command.PlayerId,
                TargetId = command.CityFactionId,
                TargetType = ReputationTargetType.Faction,
            },
            cancellationToken
        );

        var guardEncounter = await createGuardEncounter.Handle(
            new CreateGuardEncounterCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                PlayerLocationId = command.EncounterLocationId,
                LocationName = command.LocationName,
                GuardCreatureId = command.GuardCreatureId,
                GuardName = command.GuardName,
                CityFactionId = command.CityFactionId,
                ReputationScore = newReputationScore,
            },
            cancellationToken
        );

        await publishEncounterStarted.Handle(
            new PublishEncounterStartedCommand
            {
                PlayerId = command.PlayerId,
                Encounter = guardEncounter,
            },
            cancellationToken
        );

        return new SuspicionEncounterResolutionFact(
            command.EncounterId,
            SuspicionEncounterResolutionOutcome.FleeFailed,
            command.GuardName,
            command.LocationName,
            guardEncounter.Id
        );
    }

    private static EvadeParticipant ToEvadeParticipant(Creature creature) =>
        new(
            creature.Dexterity,
            creature.CurrentHp,
            creature.MaximumHp,
            creature.CurrentAp,
            creature.MaximumAp
        );
}
