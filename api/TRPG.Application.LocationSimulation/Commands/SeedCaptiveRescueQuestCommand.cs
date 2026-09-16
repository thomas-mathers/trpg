using System.Transactions;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Factions.Queries;
using TRPG.Application.Knowledge.Queries;
using TRPG.Application.Props.Commands;
using TRPG.Application.Props.Queries;
using TRPG.Application.Quests.Commands;
using TRPG.Application.Quests.Queries;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

public class SeedCaptiveRescueQuestCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
    public required int PlayerLevel { get; init; }
}

// Finds someone at the seeding location whose relative (already real, from HouseholdGenerator's
// own family graph, not a fabricated tie) can plausibly be locked away in a nearby dungeon, then
// captures that relative and offers the rescue as a quest. No-ops at any step where nothing
// eligible exists — that is the pool naturally running dry, not a failure.
internal class SeedCaptiveRescueQuestCommandHandler(
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<GetLocationIdsByCityIdQuery, IReadOnlyCollection<Guid>> getLocationIdsByCityId,
    IQueryHandler<
        GetCreatureIdsWithCreatureJobInLocationsQuery,
        IReadOnlyList<Guid>
    > getGiverCandidateIds,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    IQueryHandler<GetRescueQuestParticipantIdsQuery, IReadOnlySet<Guid>> getUsedParticipantIds,
    IQueryHandler<
        GetRelativesByCreatureIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<RelativeSummary>>
    > getRelativesByCreatureIds,
    IQueryHandler<GetRoomsByRoleQuery, IReadOnlyList<Room>> getRoomsByRole,
    IQueryHandler<GetCellLocationIdsQuery, IReadOnlySet<Guid>> getCellLocationIds,
    IQueryHandler<GetVisitedRoomLocationIdsQuery, IReadOnlySet<Guid>> getVisitedRoomLocationIds,
    IQueryHandler<GetBuildingByIdQuery, Building?> getBuildingById,
    IQueryHandler<
        GetFactionsByCreatureTypeQuery,
        IReadOnlyDictionary<CreatureType, Faction>
    > getFactionsByCreatureType,
    CreatureGenerator creatureGenerator,
    ICommandHandler<AddCreatureSpawnResultCommand> addCreatureSpawnResult,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    ICommandHandler<AddCellCommand> addCell,
    ICommandHandler<AddQuestCommand> addQuest
) : ICommandHandler<SeedCaptiveRescueQuestCommand, bool>
{
    private const int LockLevel = 20;
    private const int GoldReward = 100;
    private const int GiverReputationReward = 25;
    private const int MinimumCaptors = 2;
    private const int MaximumCaptors = 3;

    // Whoever is holding someone against their will has to want something from it — ransom, a
    // ritual, spite. An undead thing that would just kill her on sight doesn't fit; a scheming
    // captor does, independent of whatever else happens to lurk in this dungeon's ambient theme.
    private static readonly IReadOnlyList<CreatureType> CaptorCreatureTypes =
    [
        CreatureType.Goblin,
        CreatureType.Demon,
    ];

    public async Task<bool> Handle(
        SeedCaptiveRescueQuestCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var location = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = command.LocationId },
            cancellationToken
        );
        if (location == null)
        {
            return false;
        }

        var usedParticipantIds = await getUsedParticipantIds.Handle(
            new GetRescueQuestParticipantIdsQuery { WorldId = command.WorldId },
            cancellationToken
        );

        var (giver, captive) = await FindGiverAndCaptive(
            location.CityId,
            usedParticipantIds,
            cancellationToken
        );
        if (giver == null || captive == null)
        {
            return false;
        }

        var chosenRoom = await FindEligibleRoom(command, location.StateId, cancellationToken);
        if (chosenRoom == null)
        {
            return false;
        }

        var building = await getBuildingById.Handle(
            new GetBuildingByIdQuery { Id = chosenRoom.BuildingId },
            cancellationToken
        );
        if (building == null || !DungeonPopulator.SupportsDungeonType(building.BuildingType))
        {
            return false;
        }

        await Capture(command, giver, captive, chosenRoom, cancellationToken);

        return true;
    }

    private async Task<(Creature? Giver, Creature? Captive)> FindGiverAndCaptive(
        Guid? cityId,
        IReadOnlySet<Guid> usedParticipantIds,
        CancellationToken cancellationToken
    )
    {
        if (cityId == null)
        {
            return (null, null);
        }

        var cityLocationIds = await getLocationIdsByCityId.Handle(
            new GetLocationIdsByCityIdQuery { CityId = cityId.Value },
            cancellationToken
        );
        var candidateGiverIds = await getGiverCandidateIds.Handle(
            new GetCreatureIdsWithCreatureJobInLocationsQuery { LocationIds = cityLocationIds },
            cancellationToken
        );
        var candidateGivers = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = candidateGiverIds },
            cancellationToken
        );
        // Shuffled so the giver isn't always whichever eligible candidate happens to sort first
        // in a much larger city-wide pool.
        var eligibleGivers = candidateGivers
            .Values.Where(creature =>
                CreatureTypes.Humanoid.Contains(creature.CreatureType)
                && !usedParticipantIds.Contains(creature.Id)
            )
            .OrderBy(_ => Random.Shared.Next())
            .ToArray();

        var relativesByGiverId = await getRelativesByCreatureIds.Handle(
            new GetRelativesByCreatureIdsQuery
            {
                CreatureIds = eligibleGivers.Select(giver => giver.Id).ToArray(),
            },
            cancellationToken
        );

        foreach (var giver in eligibleGivers)
        {
            var eligibleRelative = relativesByGiverId
                .GetValueOrDefault(giver.Id, [])
                .FirstOrDefault(relative => !usedParticipantIds.Contains(relative.RelativeId));
            if (eligibleRelative == null)
            {
                continue;
            }

            var captive = await getCreatureById.Handle(
                new GetCreatureByIdQuery { Id = eligibleRelative.RelativeId },
                cancellationToken
            );
            if (captive == null)
            {
                continue;
            }

            return (giver, captive);
        }

        return (null, null);
    }

    private async Task<Room?> FindEligibleRoom(
        SeedCaptiveRescueQuestCommand command,
        Guid stateId,
        CancellationToken cancellationToken
    )
    {
        var cellBlockRooms = await getRoomsByRole.Handle(
            new GetRoomsByRoleQuery
            {
                WorldId = command.WorldId,
                StateId = stateId,
                Role = RoomRole.CellBlock,
            },
            cancellationToken
        );
        if (cellBlockRooms.Count == 0)
        {
            return null;
        }

        var occupiedLocationIds = await getCellLocationIds.Handle(
            new GetCellLocationIdsQuery { WorldId = command.WorldId },
            cancellationToken
        );
        var candidateRooms = cellBlockRooms
            .Where(room => !occupiedLocationIds.Contains(room.LocationId))
            .ToArray();
        if (candidateRooms.Length == 0)
        {
            return null;
        }

        var visitedLocationIds = await getVisitedRoomLocationIds.Handle(
            new GetVisitedRoomLocationIdsQuery
            {
                CreatureId = command.PlayerId,
                RoomLocationIds = candidateRooms.Select(room => room.LocationId).ToArray(),
            },
            cancellationToken
        );
        var eligibleRooms = candidateRooms
            .Where(room => !visitedLocationIds.Contains(room.LocationId))
            .ToArray();

        return eligibleRooms.Length == 0
            ? null
            : eligibleRooms[Random.Shared.Next(eligibleRooms.Length)];
    }

    private async Task Capture(
        SeedCaptiveRescueQuestCommand command,
        Creature giver,
        Creature captive,
        Room room,
        CancellationToken cancellationToken
    )
    {
        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var factionsByCreatureType = await getFactionsByCreatureType.Handle(
            new GetFactionsByCreatureTypeQuery { WorldId = command.WorldId },
            cancellationToken
        );
        var captorCount = Random.Shared.Next(MinimumCaptors, MaximumCaptors + 1);
        var captorResult = CreatureSpawnFiller.Fill(
            creatureGenerator,
            CaptorCreatureTypes,
            currentPopulation: 0,
            captorCount,
            command.PlayerLevel,
            command.WorldId,
            room.LocationId,
            Guid.NewGuid(),
            factionsByCreatureType
        );
        var keyHolder = captorResult
            .Monsters[Random.Shared.Next(captorResult.Monsters.Count)]
            .Creature;

        var key = new Key
        {
            WorldId = command.WorldId,
            Name = $"Key to {room.Name}",
            Description = $"A key that unlocks a cell in {room.Name}.",
            Quantity = 1,
            Ownership = new ItemOwnership
            {
                OwnerId = keyHolder.Id,
                OwnerType = OwnerType.Creature,
            },
        };

        await addCreatureSpawnResult.Handle(
            new AddCreatureSpawnResultCommand
            {
                Monsters = captorResult.Monsters,
                // A captor group stays perpetually on guard rather than cycling through the
                // ambient day/night schedule generated for ordinary dungeon monsters.
                Jobs = [],
                EncounterGroups = captorResult.EncounterGroups,
                EncounterGroupMembers = captorResult.EncounterGroupMembers,
                FactionMembers = captorResult.FactionMembers,
                ExtraItems = [key],
            },
            cancellationToken
        );

        await updateCreatures.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [captive.Id],
                LocationId = room.LocationId,
                State = CreatureState.Restrained,
            },
            cancellationToken
        );

        await addCell.Handle(
            new AddCellCommand
            {
                Cell = new Cell
                {
                    WorldId = command.WorldId,
                    LocationId = room.LocationId,
                    Name = "Cell",
                    Description = $"A locked cell holding {captive.Name}.",
                    CreatureId = captive.Id,
                    IsLocked = true,
                    LockLevel = LockLevel,
                    KeyItemId = key.Id,
                },
            },
            cancellationToken
        );

        var quest = new Quest
        {
            WorldId = command.WorldId,
            GiverId = giver.Id,
            Name = $"What Happened to {captive.Name}",
            Description = $"{captive.Name} never came home. Find them and bring them back safely.",
            GoldReward = GoldReward,
        };
        quest.ReputationRewards.Add(
            new QuestReputationReward
            {
                WorldId = command.WorldId,
                QuestId = quest.Id,
                TargetId = giver.Id,
                TargetType = ReputationTargetType.Creature,
                Score = GiverReputationReward,
            }
        );
        var objective = new FreeCreatureObjective
        {
            WorldId = command.WorldId,
            QuestId = quest.Id,
            Name = $"Free {captive.Name}",
            Description = $"Free {captive.Name} from captivity.",
            CreatureId = captive.Id,
            LocationId = room.LocationId,
        };
        await addQuest.Handle(
            new AddQuestCommand { Quest = quest, Objectives = [objective] },
            cancellationToken
        );

        transaction.Complete();
    }
}
