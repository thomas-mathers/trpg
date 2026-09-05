using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Reputations.Commands;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class ResolveGuardEncounterActionCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required GuardEncounterAction Action { get; init; }
    public required Guid EncounterId { get; init; }
}

internal class ResolveGuardEncounterActionCommandHandler(
    IEncountersDbContext context,
    ICommandHandler<CompleteEncounterCommand> completeEncounter,
    ICommandHandler<RemoveGoldCommand> removeGold,
    ICommandHandler<AdjustReputationsCommand> adjustReputations,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    ICommandHandler<StartFightCommand> startFight,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<GetJailForCityQuery, JailInfo?> getJailForCity,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    ICommandHandler<SetDoorTimedLockCommand> setDoorTimedLock,
    IOptionsMonitor<GuardEncounterOptions> guardEncounterOptions
) : ICommandHandler<ResolveGuardEncounterActionCommand, GuardEncounterResolutionFact>
{
    public async Task<GuardEncounterResolutionFact> Handle(
        ResolveGuardEncounterActionCommand command,
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
            PayFineEncounterAction => await ResolvePayFine(command, encounter, cancellationToken),
            GoToJailEncounterAction => await ResolveGoToJail(command, encounter, cancellationToken),
            ResistArrestEncounterAction => await ResolveResistArrest(
                command,
                encounter,
                cancellationToken
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };

        transaction.Complete();

        return fact;
    }

    private async Task<GuardEncounterResolutionFact> ResolvePayFine(
        ResolveGuardEncounterActionCommand command,
        GuardEncounter encounter,
        CancellationToken cancellationToken
    )
    {
        await removeGold.Handle(
            new RemoveGoldCommand
            {
                Owner = new ItemOwnerReference(command.PlayerId, OwnerType.Creature),
                Amount = encounter.FineAmount,
            },
            cancellationToken
        );

        await adjustReputations.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = command.PlayerId,
                WorldId = command.WorldId,
                Adjustments =
                [
                    new ReputationAdjustment(encounter.CityFactionId, -encounter.ReputationScore),
                ],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.PaidFineToGuard,
                Detail = $"Paid a {encounter.FineAmount} gold fine to the city guard",
            },
            cancellationToken
        );

        return new GuardEncounterResolutionFact(
            command.EncounterId,
            GuardEncounterResolutionOutcome.PaidFine,
            encounter.GuardName,
            encounter.LocationName!,
            encounter.FineAmount,
            null
        );
    }

    private async Task<GuardEncounterResolutionFact> ResolveGoToJail(
        ResolveGuardEncounterActionCommand command,
        GuardEncounter encounter,
        CancellationToken cancellationToken
    )
    {
        var location = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = encounter.LocationId },
            cancellationToken
        );
        if (location?.CityId == null)
        {
            throw new InvalidOperationException(
                $"Location {encounter.LocationId} has no city, cannot resolve a jail."
            );
        }

        var jail = await getJailForCity.Handle(
            new GetJailForCityQuery { CityId = location.CityId.Value },
            cancellationToken
        );
        if (jail == null)
        {
            throw new InvalidOperationException($"City {location.CityId} has no jail.");
        }

        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = command.SessionId },
            cancellationToken
        );
        var unlocksAt = playtime + GameClock.RealTimePerInGameHour * encounter.JailHours;

        await updateCreatures.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [command.PlayerId],
                LocationId = jail.CellsLocationId,
            },
            cancellationToken
        );

        await setDoorTimedLock.Handle(
            new SetDoorTimedLockCommand
            {
                DoorConnectorIds = [jail.ExitDoorConnectorId],
                UnlocksAtPlaytime = unlocksAt,
            },
            cancellationToken
        );

        var options = guardEncounterOptions.CurrentValue;
        var restoreAmount = options.ReputationThreshold + 1 - encounter.ReputationScore;

        await adjustReputations.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = command.PlayerId,
                WorldId = command.WorldId,
                Adjustments = [new ReputationAdjustment(encounter.CityFactionId, restoreAmount)],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.ServedJailTime,
                Detail = $"Served a {encounter.JailHours}-hour jail sentence",
            },
            cancellationToken
        );

        return new GuardEncounterResolutionFact(
            command.EncounterId,
            GuardEncounterResolutionOutcome.WentToJail,
            encounter.GuardName,
            encounter.LocationName!,
            null,
            encounter.JailHours
        );
    }

    private async Task<GuardEncounterResolutionFact> ResolveResistArrest(
        ResolveGuardEncounterActionCommand command,
        GuardEncounter encounter,
        CancellationToken cancellationToken
    )
    {
        await updateCreatures.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [encounter.GuardCreatureId],
                State = CreatureState.Alerted,
            },
            cancellationToken
        );

        await startFight.Handle(
            new StartFightCommand
            {
                SessionId = command.SessionId,
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                EnemyCreatureIds = [encounter.GuardCreatureId],
                HasSurpriseRound = false,
            },
            cancellationToken
        );

        return new GuardEncounterResolutionFact(
            command.EncounterId,
            GuardEncounterResolutionOutcome.ResistedArrest,
            encounter.GuardName,
            encounter.LocationName!,
            null,
            null
        );
    }

    private async Task<GuardEncounter> GetEncounter(
        ResolveGuardEncounterActionCommand command,
        CancellationToken cancellationToken
    )
    {
        var encounter = await context
            .Encounters.OfType<GuardEncounter>()
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
            throw new EntityNotFoundException(nameof(GuardEncounter), command.EncounterId);
        }
        if (encounter.State != EncounterState.Active)
        {
            throw new InvalidOperationException("The guard encounter has already been resolved.");
        }

        return encounter;
    }
}
