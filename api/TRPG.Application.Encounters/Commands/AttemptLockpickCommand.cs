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
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Reputations.Queries;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public enum LockpickAttemptOutcome
{
    NothingToPick,
    Failed,
    Opened,
}

public record AttemptLockpickResult(LockpickAttemptOutcome Outcome, Encounter? Encounter = null);

public class AttemptLockpickCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid ConnectorId { get; init; }
    public required Guid DestinationLocationId { get; init; }
}

internal class AttemptLockpickCommandHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<
        GetDoorConnectorsByConnectorIdsQuery,
        IReadOnlyDictionary<Guid, DoorConnector>
    > getDoorConnectorsByConnectorIds,
    SkillCheckService skillCheckService,
    SneakDetectionService sneakDetectionService,
    ICommandHandler<AdjustCreatureSkillsCommand> adjustCreatureSkills,
    IQueryHandler<GetGuardAtLocationQuery, Creature?> getGuardAtLocation,
    IQueryHandler<
        GetLiveHumanoidWitnessesAtLocationQuery,
        IReadOnlyCollection<LiveHumanoidWitness>
    > getLiveHumanoidWitnessesAtLocation,
    IQueryHandler<GetBuildingByLocationIdQuery, BuildingIdentity?> getBuildingByLocationId,
    IQueryHandler<
        GetBuildingOwnersByBuildingIdQuery,
        IReadOnlyCollection<BuildingOwner>
    > getBuildingOwnersByBuildingId,
    ICommandHandler<AddItemsCommand> addItems,
    ICommandHandler<AddDoorConnectorKeyCommand> addDoorConnectorKey,
    IQueryHandler<GetCityFactionForCreatureQuery, Guid?> getCityFactionForCreature,
    IQueryHandler<GetReputationScoreQuery, int> getReputationScore,
    ICommandHandler<AddLockpickingCrimesCommand> addLockpickingCrimes,
    ICommandHandler<AddCrimeWitnessesCommand> addCrimeWitnesses,
    ICommandHandler<
        EvaluateTrespassingEncounterCommand,
        HostileEncounter?
    > evaluateTrespassingEncounter,
    ICommandHandler<CreateGuardEncounterCommand, GuardEncounter> createGuardEncounter,
    ICommandHandler<PublishEncounterStartedCommand> publishEncounterStarted,
    IOptionsMonitor<LockpickingOptions> lockpickingOptions
) : ICommandHandler<AttemptLockpickCommand, AttemptLockpickResult>
{
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

        var outcome = opened ? LockpickAttemptOutcome.Opened : LockpickAttemptOutcome.Failed;

        var building = await getBuildingByLocationId.Handle(
            new GetBuildingByLocationIdQuery { LocationId = command.DestinationLocationId },
            cancellationToken
        );
        if (building != null && await PlayerOwnsBuilding(building.Id, player.Id, cancellationToken))
        {
            transaction.Complete();
            return new AttemptLockpickResult(outcome);
        }

        var crime =
            opened && building != null
                ? await RecordBreakIn(command, player, door, building, cancellationToken)
                : null;

        var currentLocation = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = player.LocationId },
            cancellationToken
        );
        Encounter? encounter = null;
        if (opened && door.UnlocksAtPlaytime != null)
        {
            encounter = await ResolveJailbreak(
                command,
                player,
                currentLocation!,
                crime,
                cancellationToken
            );
        }
        else if (currentLocation!.RoomId == null)
        {
            if (building != null)
            {
                encounter = await EvaluateExteriorDetection(
                    command,
                    player,
                    currentLocation,
                    building,
                    crime,
                    cancellationToken
                );
            }
        }
        else
        {
            encounter = await evaluateTrespassingEncounter.Handle(
                new EvaluateTrespassingEncounterCommand
                {
                    WorldId = command.WorldId,
                    PlayerId = player.Id,
                },
                cancellationToken
            );
        }

        await publishEncounterStarted.Handle(
            new PublishEncounterStartedCommand { PlayerId = player.Id, Encounter = encounter },
            cancellationToken
        );

        transaction.Complete();

        return new AttemptLockpickResult(outcome, encounter);
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

    private async Task<bool> PlayerOwnsBuilding(
        Guid buildingId,
        Guid playerId,
        CancellationToken cancellationToken
    )
    {
        var owners = await getBuildingOwnersByBuildingId.Handle(
            new GetBuildingOwnersByBuildingIdQuery { BuildingId = buildingId },
            cancellationToken
        );
        return owners.Any(owner => owner.OwnerId == playerId);
    }

    private async Task<LockpickingCrime> RecordBreakIn(
        AttemptLockpickCommand command,
        Creature player,
        DoorConnector door,
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
                    DoorConnectorId = door.Id,
                    WorldId = command.WorldId,
                },
            },
            cancellationToken
        );

        var crime = new LockpickingCrime
        {
            WorldId = command.WorldId,
            PlayerId = player.Id,
            LocationId = player.LocationId,
            BuildingId = building.Id,
            BuildingName = building.Name,
            OwnerFactionId = building.FactionId,
            IsJailbreak = door.UnlocksAtPlaytime != null,
        };

        await addLockpickingCrimes.Handle(
            new AddLockpickingCrimesCommand { Crimes = [crime] },
            cancellationToken
        );

        return crime;
    }

    // A timed lock is only ever set when a sentence starts, so picking one is a jailbreak.
    private async Task<GuardEncounter?> ResolveJailbreak(
        AttemptLockpickCommand command,
        Creature player,
        Location currentLocation,
        LockpickingCrime? crime,
        CancellationToken cancellationToken
    )
    {
        var guard = await getGuardAtLocation.Handle(
            new GetGuardAtLocationQuery
            {
                WorldId = command.WorldId,
                LocationId = command.DestinationLocationId,
            },
            cancellationToken
        );
        if (guard == null)
        {
            return null;
        }

        // The empty cell is evidence, so the jailer finds out whether or not they saw it happen.
        if (crime != null)
        {
            await addCrimeWitnesses.Handle(
                new AddCrimeWitnessesCommand
                {
                    WorldId = command.WorldId,
                    CrimeIds = [crime.Id],
                    WitnessCreatureIds = [guard.Id],
                },
                cancellationToken
            );
        }

        var isDetected = await sneakDetectionService.RollDetection(
            command.WorldId,
            player.Id,
            player.IsSneaking,
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

        var score = await getReputationScore.Handle(
            new GetReputationScoreQuery
            {
                CreatureId = player.Id,
                TargetId = cityFactionId,
                TargetType = ReputationTargetType.Faction,
            },
            cancellationToken
        );

        return await createGuardEncounter.Handle(
            new CreateGuardEncounterCommand
            {
                WorldId = command.WorldId,
                PlayerId = player.Id,
                PlayerLocationId = player.LocationId,
                LocationName = currentLocation.Name,
                GuardCreatureId = guard.Id,
                GuardName = guard.Name,
                CityFactionId = cityFactionId,
                ReputationScore = score,
                TriggeringCrimeId = crime?.Id,
            },
            cancellationToken
        );
    }

    private async Task<GuardEncounter?> EvaluateExteriorDetection(
        AttemptLockpickCommand command,
        Creature player,
        Location currentLocation,
        BuildingIdentity building,
        LockpickingCrime? existingCrime,
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

        var isDetected = await sneakDetectionService.RollDetection(
            command.WorldId,
            player.Id,
            player.IsSneaking,
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

        var crime = existingCrime;
        if (crime == null)
        {
            crime = new LockpickingCrime
            {
                WorldId = command.WorldId,
                PlayerId = player.Id,
                LocationId = player.LocationId,
                BuildingId = building.Id,
                BuildingName = building.Name,
                OwnerFactionId = building.FactionId,
            };
            await addLockpickingCrimes.Handle(
                new AddLockpickingCrimesCommand { Crimes = [crime] },
                cancellationToken
            );
        }

        var bystanders = await getLiveHumanoidWitnessesAtLocation.Handle(
            new GetLiveHumanoidWitnessesAtLocationQuery
            {
                WorldId = command.WorldId,
                LocationId = player.LocationId,
                ExcludeCreatureId = player.Id,
            },
            cancellationToken
        );

        // The guard is the one who confronts, but anyone still standing there saw it too.
        await addCrimeWitnesses.Handle(
            new AddCrimeWitnessesCommand
            {
                WorldId = command.WorldId,
                CrimeIds = [crime.Id],
                WitnessCreatureIds = bystanders
                    .Select(bystander => bystander.Id)
                    .Append(guard.Id)
                    .Distinct()
                    .ToArray(),
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

        return await createGuardEncounter.Handle(
            new CreateGuardEncounterCommand
            {
                WorldId = command.WorldId,
                PlayerId = player.Id,
                PlayerLocationId = player.LocationId,
                LocationName = currentLocation.Name,
                GuardCreatureId = guard.Id,
                GuardName = guard.Name,
                CityFactionId = cityFactionId,
                ReputationScore = score,
                TriggeringCrimeId = crime.Id,
            },
            cancellationToken
        );
    }
}
