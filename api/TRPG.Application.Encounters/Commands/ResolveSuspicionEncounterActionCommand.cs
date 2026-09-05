using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Combat;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Reputations.Commands;
using TRPG.Application.Reputations.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class ResolveSuspicionEncounterActionCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required SuspicionEncounterAction Action { get; init; }
    public required Guid EncounterId { get; init; }
}

internal class ResolveSuspicionEncounterActionCommandHandler(
    IEncountersDbContext context,
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

        var encounter = await GetEncounter(command, cancellationToken);

        await completeEncounter.Handle(
            new CompleteEncounterCommand { EncounterId = command.EncounterId },
            cancellationToken
        );

        var fact = command.Action switch
        {
            ComplySuspicionAction => await ResolveComply(command, encounter, cancellationToken),
            FleeSuspicionAction => await ResolveFlee(command, encounter, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };

        transaction.Complete();

        return fact;
    }

    private async Task<SuspicionEncounterResolutionFact> ResolveComply(
        ResolveSuspicionEncounterActionCommand command,
        SuspicionEncounter encounter,
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
                        encounter.CityFactionId,
                        -options.ComplyReputationPenalty
                    ),
                ],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.CaughtSneaking,
                Detail = $"Caught sneaking near {encounter.GuardName}",
            },
            cancellationToken
        );

        return new SuspicionEncounterResolutionFact(
            command.EncounterId,
            SuspicionEncounterResolutionOutcome.Complied,
            encounter.GuardName,
            encounter.LocationName!,
            null
        );
    }

    private async Task<SuspicionEncounterResolutionFact> ResolveFlee(
        ResolveSuspicionEncounterActionCommand command,
        SuspicionEncounter encounter,
        CancellationToken cancellationToken
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );
        if (player == null)
        {
            throw new EntityNotFoundException(nameof(Creature), command.PlayerId);
        }

        var guard = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = encounter.GuardCreatureId },
            cancellationToken
        );
        if (guard == null)
        {
            throw new EntityNotFoundException(nameof(Creature), encounter.GuardCreatureId);
        }

        var catchChance = EvadeChanceCalculator.CatchChance(
            fleeOptions.Value,
            ToEvadeParticipant(player),
            [ToEvadeParticipant(guard)]
        );
        var isCaught = Random.Shared.NextDouble() < catchChance;

        if (!isCaught)
        {
            return new SuspicionEncounterResolutionFact(
                command.EncounterId,
                SuspicionEncounterResolutionOutcome.Fled,
                encounter.GuardName,
                encounter.LocationName!,
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
                        encounter.CityFactionId,
                        -options.FleeFailedReputationPenalty
                    ),
                ],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.CaughtFleeingSuspicion,
                Detail = $"Fled from {encounter.GuardName}'s questioning and was caught",
            },
            cancellationToken
        );

        var newReputationScore = await getReputationScore.Handle(
            new GetReputationScoreQuery
            {
                CreatureId = command.PlayerId,
                TargetId = encounter.CityFactionId,
                TargetType = ReputationTargetType.Faction,
            },
            cancellationToken
        );

        var guardEncounter = await createGuardEncounter.Handle(
            new CreateGuardEncounterCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                PlayerLocationId = encounter.LocationId,
                LocationName = encounter.LocationName!,
                GuardCreatureId = encounter.GuardCreatureId,
                GuardName = encounter.GuardName,
                CityFactionId = encounter.CityFactionId,
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
            encounter.GuardName,
            encounter.LocationName!,
            guardEncounter.Id
        );
    }

    private async Task<SuspicionEncounter> GetEncounter(
        ResolveSuspicionEncounterActionCommand command,
        CancellationToken cancellationToken
    )
    {
        var encounter = await context
            .Encounters.OfType<SuspicionEncounter>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item =>
                    item.Id == command.EncounterId
                    && item.WorldId == command.WorldId
                    && item.PlayerId == command.PlayerId,
                cancellationToken
            );
        if (encounter == null)
        {
            throw new EntityNotFoundException(nameof(SuspicionEncounter), command.EncounterId);
        }
        if (encounter.State != EncounterState.Active)
        {
            throw new InvalidOperationException(
                "The suspicion encounter has already been resolved."
            );
        }

        return encounter;
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
