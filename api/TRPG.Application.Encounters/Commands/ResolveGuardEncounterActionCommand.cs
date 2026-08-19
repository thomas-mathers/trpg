using System.Transactions;
using Microsoft.Extensions.Options;
using TRPG.Application.Buildings.Commands;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Reputations.Commands;
using TRPG.Application.Reputations.Queries;
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
    public required Guid EncounterLocationId { get; init; }
}

internal class ResolveGuardEncounterActionCommandHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<GetCityFactionForCreatureQuery, Guid?> getCityFactionForCreature,
    IQueryHandler<GetReputationScoreQuery, int> getReputationScore,
    ICommandHandler<AdjustReputationCommand> adjustReputation,
    ICommandHandler<RemoveGoldCommand> removeGold,
    ICommandHandler<CompleteEncounterCommand> completeEncounter,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    ICommandHandler<StartFightCommand> startFight,
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

        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );
        var guard = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.GuardCreatureId },
            cancellationToken
        );
        var location = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = command.EncounterLocationId },
            cancellationToken
        );
        var cityFactionId = await getCityFactionForCreature.Handle(
            new GetCityFactionForCreatureQuery { CreatureId = command.GuardCreatureId },
            cancellationToken
        );

        if (cityFactionId == null)
        {
            throw new InvalidOperationException(
                $"Guard {command.GuardCreatureId} has no city faction membership."
            );
        }

        var fact = command.Action switch
        {
            PayFineEncounterAction => await ResolvePayFine(
                command,
                player!,
                guard!,
                location!,
                cityFactionId.Value,
                cancellationToken
            ),
            GoToJailEncounterAction => await ResolveGoToJail(
                command,
                player!,
                guard!,
                location!,
                cityFactionId.Value,
                cancellationToken
            ),
            ResistArrestEncounterAction => await ResolveResistArrest(
                command,
                guard!,
                location!,
                cancellationToken
            ),
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
        Creature player,
        Creature guard,
        Location location,
        Guid cityFactionId,
        CancellationToken cancellationToken
    )
    {
        var options = guardEncounterOptions.CurrentValue;
        var score = await getReputationScore.Handle(
            new GetReputationScoreQuery
            {
                CreatureId = command.PlayerId,
                TargetId = cityFactionId,
                TargetType = ReputationTargetType.Faction,
            },
            cancellationToken
        );
        var fineAmount = GuardEncounterCalculator.ComputeFineGold(score, options);

        await removeGold.Handle(
            new RemoveGoldCommand
            {
                Owner = new ItemOwnerReference(player.Id, OwnerType.Creature),
                Amount = fineAmount,
            },
            cancellationToken
        );

        await adjustReputation.Handle(
            new AdjustReputationCommand
            {
                CreatureId = command.PlayerId,
                TargetId = cityFactionId,
                TargetType = ReputationTargetType.Faction,
                DeltaScore = -score,
                Reason = "Paid a fine to the city guard",
            },
            cancellationToken
        );

        return new GuardEncounterResolutionFact(
            command.EncounterId,
            GuardEncounterResolutionOutcome.PaidFine,
            guard.Name,
            location.Name,
            fineAmount,
            null
        );
    }

    private async Task<GuardEncounterResolutionFact> ResolveGoToJail(
        ResolveGuardEncounterActionCommand command,
        Creature player,
        Creature guard,
        Location location,
        Guid cityFactionId,
        CancellationToken cancellationToken
    )
    {
        var options = guardEncounterOptions.CurrentValue;
        var score = await getReputationScore.Handle(
            new GetReputationScoreQuery
            {
                CreatureId = command.PlayerId,
                TargetId = cityFactionId,
                TargetType = ReputationTargetType.Faction,
            },
            cancellationToken
        );
        var jailHours = GuardEncounterCalculator.ComputeJailHours(score, options);

        if (location.CityId == null)
        {
            throw new InvalidOperationException(
                $"Location {location.Id} has no city, cannot resolve a jail."
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
        var unlocksAt = playtime + GameClock.RealTimePerInGameHour * jailHours;

        await updateCreatures.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [player.Id],
                LocationId = jail.CellsLocationId,
            },
            cancellationToken
        );

        await setDoorTimedLock.Handle(
            new SetDoorTimedLockCommand
            {
                DoorConnectorIds = jail.CellDoorConnectorIds,
                UnlocksAtPlaytime = unlocksAt,
            },
            cancellationToken
        );

        return new GuardEncounterResolutionFact(
            command.EncounterId,
            GuardEncounterResolutionOutcome.WentToJail,
            guard.Name,
            location.Name,
            null,
            jailHours
        );
    }

    private async Task<GuardEncounterResolutionFact> ResolveResistArrest(
        ResolveGuardEncounterActionCommand command,
        Creature guard,
        Location location,
        CancellationToken cancellationToken
    )
    {
        await updateCreatures.Handle(
            new UpdateCreaturesCommand { CreatureIds = [guard.Id], State = CreatureState.Alerted },
            cancellationToken
        );

        await startFight.Handle(
            new StartFightCommand
            {
                SessionId = command.SessionId,
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                EnemyCreatureIds = [guard.Id],
                EncounterId = command.EncounterId,
            },
            cancellationToken
        );

        return new GuardEncounterResolutionFact(
            command.EncounterId,
            GuardEncounterResolutionOutcome.ResistedArrest,
            guard.Name,
            location.Name,
            null,
            null
        );
    }
}
