using System.Transactions;
using Microsoft.Extensions.Options;
using TRPG.Application.Buildings.Commands;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Reputations.Commands;
using TRPG.Application.Worlds.Queries;
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
    public required Guid GuardCreatureId { get; init; }
    public required Guid CityFactionId { get; init; }
    public required string GuardName { get; init; }
    public required Guid EncounterLocationId { get; init; }
    public required string LocationName { get; init; }
    public required int ReputationScore { get; init; }
    public required int FineAmount { get; init; }
    public required int JailHours { get; init; }
}

internal class ResolveGuardEncounterActionCommandHandler(
    ICommandHandler<CompleteEncounterCommand> completeEncounter,
    ICommandHandler<RemoveGoldCommand> removeGold,
    ICommandHandler<AdjustReputationsCommand> adjustReputations,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    ICommandHandler<StartFightCommand> startFight,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<GetJailForCityQuery, JailInfo?> getJailForCity,
    ICommandHandler<SetDoorTimedLockCommand> setDoorTimedLock,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
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

        var fact = command.Action switch
        {
            PayFineEncounterAction => await ResolvePayFine(command, cancellationToken),
            GoToJailEncounterAction => await ResolveGoToJail(command, cancellationToken),
            ResistArrestEncounterAction => await ResolveResistArrest(command, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };

        await completeEncounter.Handle(
            new CompleteEncounterCommand { EncounterId = command.EncounterId },
            cancellationToken
        );

        transaction.Complete();

        return fact;
    }

    private async Task<GuardEncounterResolutionFact> ResolvePayFine(
        ResolveGuardEncounterActionCommand command,
        CancellationToken cancellationToken
    )
    {
        await removeGold.Handle(
            new RemoveGoldCommand
            {
                Owner = new ItemOwnerReference(command.PlayerId, OwnerType.Creature),
                Amount = command.FineAmount,
            },
            cancellationToken
        );

        await adjustReputations.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = command.PlayerId,
                Adjustments =
                [
                    new ReputationAdjustment(command.CityFactionId, -command.ReputationScore),
                ],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.PaidFineToGuard,
                Detail = $"Paid a {command.FineAmount} gold fine to the city guard",
            },
            cancellationToken
        );

        return new GuardEncounterResolutionFact(
            command.EncounterId,
            GuardEncounterResolutionOutcome.PaidFine,
            command.GuardName,
            command.LocationName,
            command.FineAmount,
            null
        );
    }

    private async Task<GuardEncounterResolutionFact> ResolveGoToJail(
        ResolveGuardEncounterActionCommand command,
        CancellationToken cancellationToken
    )
    {
        var location = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = command.EncounterLocationId },
            cancellationToken
        );
        if (location?.CityId == null)
        {
            throw new InvalidOperationException(
                $"Location {command.EncounterLocationId} has no city, cannot resolve a jail."
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
        var unlocksAt = playtime + GameClock.RealTimePerInGameHour * command.JailHours;

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
        var restoreAmount = options.ReputationThreshold + 1 - command.ReputationScore;

        await adjustReputations.Handle(
            new AdjustReputationsCommand
            {
                CreatureId = command.PlayerId,
                Adjustments = [new ReputationAdjustment(command.CityFactionId, restoreAmount)],
                TargetType = ReputationTargetType.Faction,
                Reason = ReputationReason.ServedJailTime,
                Detail = $"Served a {command.JailHours}-hour jail sentence",
            },
            cancellationToken
        );

        return new GuardEncounterResolutionFact(
            command.EncounterId,
            GuardEncounterResolutionOutcome.WentToJail,
            command.GuardName,
            command.LocationName,
            null,
            command.JailHours
        );
    }

    private async Task<GuardEncounterResolutionFact> ResolveResistArrest(
        ResolveGuardEncounterActionCommand command,
        CancellationToken cancellationToken
    )
    {
        await updateCreatures.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [command.GuardCreatureId],
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
                EnemyCreatureIds = [command.GuardCreatureId],
            },
            cancellationToken
        );

        return new GuardEncounterResolutionFact(
            command.EncounterId,
            GuardEncounterResolutionOutcome.ResistedArrest,
            command.GuardName,
            command.LocationName,
            null,
            null
        );
    }
}
