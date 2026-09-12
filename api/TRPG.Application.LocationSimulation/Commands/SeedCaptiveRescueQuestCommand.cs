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
    IQueryHandler<
        GetCreatureIdsWithCreatureJobInLocationQuery,
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
    DungeonPopulator dungeonPopulator,
    ICommandHandler<AddCreatureSpawnResultCommand> addCreatureSpawnResult,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    ICommandHandler<AddCellCommand> addCell,
    ICommandHandler<AddQuestCommand> addQuest
) : ICommandHandler<SeedCaptiveRescueQuestCommand, bool>
{
    private const int LockLevel = 20;
    private const int GoldReward = 100;
    private const int GiverReputationReward = 25;

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
            command,
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

        await Capture(command, giver, captive, chosenRoom, building, cancellationToken);

        return true;
    }

    private async Task<(Creature? Giver, Creature? Captive)> FindGiverAndCaptive(
        SeedCaptiveRescueQuestCommand command,
        IReadOnlySet<Guid> usedParticipantIds,
        CancellationToken cancellationToken
    )
    {
        var candidateGiverIds = await getGiverCandidateIds.Handle(
            new GetCreatureIdsWithCreatureJobInLocationQuery { LocationId = command.LocationId },
            cancellationToken
        );
        var candidateGivers = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = candidateGiverIds },
            cancellationToken
        );
        var eligibleGivers = candidateGivers
            .Values.Where(creature =>
                CreatureTypes.Humanoid.Contains(creature.CreatureType)
                && !usedParticipantIds.Contains(creature.Id)
            )
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
        Building building,
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
        var guardResult = dungeonPopulator.GenerateForced(
            command.WorldId,
            room.LocationId,
            building.BuildingType,
            command.PlayerLevel,
            factionsByCreatureType
        );
        var guardCreature = guardResult.Monsters.Single().Creature;

        var key = new Key
        {
            WorldId = command.WorldId,
            Name = $"Key to {room.Name}",
            Description = $"A key that unlocks a cell in {room.Name}.",
            Quantity = 1,
            Ownership = new ItemOwnership
            {
                OwnerId = guardCreature.Id,
                OwnerType = OwnerType.Creature,
            },
        };

        await addCreatureSpawnResult.Handle(
            new AddCreatureSpawnResultCommand
            {
                Monsters = guardResult.Monsters,
                Jobs = guardResult.Jobs,
                EncounterGroups = guardResult.EncounterGroups,
                EncounterGroupMembers = guardResult.EncounterGroupMembers,
                FactionMembers = guardResult.FactionMembers,
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
