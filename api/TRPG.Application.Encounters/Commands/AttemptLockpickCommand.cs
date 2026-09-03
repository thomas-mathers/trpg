using System.Transactions;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Crimes.Commands;
using TRPG.Application.Encounters;
using TRPG.Application.Factions.Queries;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Reputations.Mappers;
using TRPG.Application.Reputations.Queries;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public enum LockpickAttemptOutcome
{
    NothingToPick,
    Failed,
    Opened,
}

public record AttemptLockpickResult(
    LockpickAttemptOutcome Outcome,
    GuardEncounter? GuardEncounter = null,
    HostileEncounter? HostileEncounter = null
);

public class AttemptLockpickCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid ConnectorId { get; init; }
    public required Guid DestinationLocationId { get; init; }
}

internal class AttemptLockpickCommandHandler(
    IEncountersDbContext context,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<
        GetDoorConnectorsByConnectorIdsQuery,
        IReadOnlyDictionary<Guid, DoorConnector>
    > getDoorConnectorsByConnectorIds,
    SkillCheckService skillCheckService,
    ICommandHandler<AdjustCreatureSkillsCommand> adjustCreatureSkills,
    IQueryHandler<GetGuardAtLocationQuery, Creature?> getGuardAtLocation,
    IQueryHandler<GetBuildingByLocationIdQuery, BuildingIdentity?> getBuildingByLocationId,
    IQueryHandler<
        GetBuildingOwnersByBuildingIdQuery,
        IReadOnlyCollection<BuildingOwner>
    > getBuildingOwnersByBuildingId,
    ICommandHandler<SetTrespassingBuildingCommand> setTrespassingBuilding,
    ICommandHandler<AddItemsCommand> addItems,
    ICommandHandler<AddDoorConnectorKeyCommand> addDoorConnectorKey,
    IQueryHandler<GetCityFactionForCreatureQuery, Guid?> getCityFactionForCreature,
    IQueryHandler<GetReputationScoreQuery, int> getReputationScore,
    IQueryHandler<
        GetRecentReputationLogQuery,
        IReadOnlyCollection<ReputationLogEntry>
    > getRecentReputationLog,
    ICommandHandler<AddBreakingAndEnteringCrimesCommand> addBreakingAndEnteringCrimes,
    ICommandHandler<AddCrimeWitnessesCommand> addCrimeWitnesses,
    ICommandHandler<
        EvaluateTrespassingEncounterCommand,
        HostileEncounter?
    > evaluateTrespassingEncounter,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted,
    IOptionsMonitor<LockpickingOptions> lockpickingOptions,
    IOptionsMonitor<GuardEncounterOptions> guardEncounterOptions
) : ICommandHandler<AttemptLockpickCommand, AttemptLockpickResult>
{
    private const int RecentOffenseLimit = 3;

    public async Task<AttemptLockpickResult> Handle(
        AttemptLockpickCommand command,
        CancellationToken cancellationToken = default
    )
    {
        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var door = await GetLockedDoor(command.ConnectorId, cancellationToken);
        if (door == null)
        {
            transaction.Complete();
            return new AttemptLockpickResult(LockpickAttemptOutcome.NothingToPick);
        }

        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );

        var opened = await skillCheckService.Roll(
            player!.Id,
            Skill.Lockpicking,
            LockpickingChanceCalculator.BuildLockOpenCurve(
                door.LockLevel,
                lockpickingOptions.CurrentValue
            ),
            cancellationToken
        );

        if (opened)
        {
            await adjustCreatureSkills.Handle(
                new AdjustCreatureSkillsCommand
                {
                    WorldId = command.WorldId,
                    CreatureId = player.Id,
                    UsageCounts = new Dictionary<Skill, int> { [Skill.Lockpicking] = 1 },
                },
                cancellationToken
            );
        }

        // Owning the destination building, or there being no building there at all, means opening
        // this specific door can never itself be a crime - though the room the player is already
        // standing in (checked below) is a separate question with its own authorization.
        var building = await GetBuildingIfNotOwnedByPlayer(
            command.DestinationLocationId,
            player.Id,
            cancellationToken
        );

        var crime =
            opened && building != null
                ? await RecordBreakIn(command, player, door.Id, building, cancellationToken)
                : null;

        var currentLocation = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = player.LocationId },
            cancellationToken
        );
        var encounter = await DetectPlayer(
            command,
            player,
            currentLocation!,
            building,
            crime,
            cancellationToken
        );

        await publishEncounterStarted.Handle(
            new PublishEncounterStartedCommand { PlayerId = player.Id, Encounter = encounter },
            cancellationToken
        );

        transaction.Complete();

        return new AttemptLockpickResult(
            opened ? LockpickAttemptOutcome.Opened : LockpickAttemptOutcome.Failed,
            encounter as GuardEncounter,
            encounter as HostileEncounter
        );
    }

    private async Task<DoorConnector?> GetLockedDoor(
        Guid connectorId,
        CancellationToken cancellationToken
    )
    {
        var doorsByConnectorId = await getDoorConnectorsByConnectorIds.Handle(
            new GetDoorConnectorsByConnectorIdsQuery { ConnectorIds = [connectorId] },
            cancellationToken
        );
        return doorsByConnectorId.TryGetValue(connectorId, out var door) && door.IsLocked
            ? door
            : null;
    }

    private async Task<BuildingIdentity?> GetBuildingIfNotOwnedByPlayer(
        Guid destinationLocationId,
        Guid playerId,
        CancellationToken cancellationToken
    )
    {
        var building = await getBuildingByLocationId.Handle(
            new GetBuildingByLocationIdQuery { LocationId = destinationLocationId },
            cancellationToken
        );
        if (building == null)
        {
            return null;
        }

        var owners = await getBuildingOwnersByBuildingId.Handle(
            new GetBuildingOwnersByBuildingIdQuery { BuildingId = building.Id },
            cancellationToken
        );

        return owners.Any(owner => owner.OwnerId == playerId) ? null : building;
    }

    private async Task<Encounter?> DetectPlayer(
        AttemptLockpickCommand command,
        Creature player,
        Location currentLocation,
        BuildingIdentity? building,
        BreakingAndEnteringCrime? crime,
        CancellationToken cancellationToken
    )
    {
        if (currentLocation.RoomId == null)
        {
            // A door with no unauthorized building behind it can't be witnessed as a break-in.
            return building == null
                ? null
                : await EvaluateExteriorDetection(
                    command,
                    player,
                    currentLocation,
                    building,
                    crime,
                    cancellationToken
                );
        }

        // Whether the player's current room is unauthorized doesn't depend on what this specific door leads to.
        return await evaluateTrespassingEncounter.Handle(
            new EvaluateTrespassingEncounterCommand
            {
                WorldId = command.WorldId,
                PlayerId = player.Id,
            },
            cancellationToken
        );
    }

    private async Task<BreakingAndEnteringCrime> RecordBreakIn(
        AttemptLockpickCommand command,
        Creature player,
        Guid doorConnectorRowId,
        BuildingIdentity building,
        CancellationToken cancellationToken
    )
    {
        var pickedLockKey = new Key
        {
            WorldId = command.WorldId,
            Name = $"Forced entry — {building.Name}",
            Description = $"Proof of a forced entry into {building.Name}.",
            Quantity = 1,
            CanTrade = false,
            IsHidden = true,
            Ownership = new ItemOwnership { OwnerId = player.Id, OwnerType = OwnerType.Creature },
        };

        await addItems.Handle(new AddItemsCommand { Items = [pickedLockKey] }, cancellationToken);

        await addDoorConnectorKey.Handle(
            new AddDoorConnectorKeyCommand
            {
                DoorConnectorKey = new DoorConnectorKey
                {
                    ItemId = pickedLockKey.Id,
                    DoorConnectorId = doorConnectorRowId,
                    WorldId = command.WorldId,
                },
            },
            cancellationToken
        );

        var crime = new BreakingAndEnteringCrime
        {
            WorldId = command.WorldId,
            PlayerId = player.Id,
            LocationId = player.LocationId,
            BuildingId = building.Id,
            BuildingName = building.Name,
            OwnerFactionId = building.FactionId,
        };

        await addBreakingAndEnteringCrimes.Handle(
            new AddBreakingAndEnteringCrimesCommand { Crimes = [crime] },
            cancellationToken
        );

        await setTrespassingBuilding.Handle(
            new SetTrespassingBuildingCommand
            {
                WorldId = command.WorldId,
                BuildingId = building.Id,
            },
            cancellationToken
        );

        return crime;
    }

    private async Task<GuardEncounter?> EvaluateExteriorDetection(
        AttemptLockpickCommand command,
        Creature player,
        Location currentLocation,
        BuildingIdentity building,
        BreakingAndEnteringCrime? crime,
        CancellationToken cancellationToken
    )
    {
        var guard = await getGuardAtLocation.Handle(
            new GetGuardAtLocationQuery
            {
                WorldId = command.WorldId,
                LocationId = player.LocationId,
            },
            cancellationToken
        );
        if (guard == null)
        {
            return null;
        }

        var isDetected = await skillCheckService.Roll(
            player.Id,
            Skill.Sneak,
            LockpickingChanceCalculator.BuildDetectionCurve(lockpickingOptions.CurrentValue),
            cancellationToken
        );
        if (!isDetected)
        {
            return null;
        }

        var cityFactionId =
            await getCityFactionForCreature.Handle(
                new GetCityFactionForCreatureQuery { CreatureId = guard.Id },
                cancellationToken
            )
            ?? throw new InvalidOperationException(
                $"Guard {guard.Id} has no city faction membership."
            );

        // Reaching here means the pick failed - a successful one already recorded the crime via RecordBreakIn.
        if (crime == null)
        {
            crime = new BreakingAndEnteringCrime
            {
                WorldId = command.WorldId,
                PlayerId = player.Id,
                LocationId = player.LocationId,
                BuildingId = building.Id,
                BuildingName = building.Name,
                OwnerFactionId = building.FactionId,
            };
            await addBreakingAndEnteringCrimes.Handle(
                new AddBreakingAndEnteringCrimesCommand { Crimes = [crime] },
                cancellationToken
            );
        }

        await addCrimeWitnesses.Handle(
            new AddCrimeWitnessesCommand
            {
                WorldId = command.WorldId,
                CrimeIds = [crime.Id],
                WitnessCreatureIds = [guard.Id],
            },
            cancellationToken
        );

        var score = await getReputationScore.Handle(
            new GetReputationScoreQuery
            {
                CreatureId = player.Id,
                TargetId = cityFactionId,
                TargetType = ReputationTargetType.Faction,
            },
            cancellationToken
        );

        var recentOffenses = await getRecentReputationLog.Handle(
            new GetRecentReputationLogQuery
            {
                CreatureId = player.Id,
                Targets = [new ReputationLogTarget(cityFactionId, ReputationTargetType.Faction)],
                Limit = RecentOffenseLimit,
                NegativeOnly = true,
            },
            cancellationToken
        );

        var options = guardEncounterOptions.CurrentValue;
        var encounter = new GuardEncounter
        {
            WorldId = command.WorldId,
            PlayerId = player.Id,
            LocationId = player.LocationId,
            LocationName = currentLocation.Name,
            GuardCreatureId = guard.Id,
            CityFactionId = cityFactionId,
            GuardName = guard.Name,
            ReputationScore = score,
            FineAmount = GuardEncounterCalculator.ComputeFineGold(score, options),
            JailHours = GuardEncounterCalculator.ComputeJailHours(score, options),
            RecentOffenses = recentOffenses
                .Select(entry => entry.Detail ?? entry.Reason.ToDisplayText())
                .ToList(),
        };
        context.Encounters.Add(encounter);
        await context.SaveChangesAsync(cancellationToken);

        return encounter;
    }
}
