using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.Quests.Commands;
using TRPG.Application.Quests.Queries;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

public class SeedAssassinateQuestCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
    public required int PlayerLevel { get; init; }
}

// Finds someone at the seeding location who wants a nearby dungeon's boss-room occupant taken out
// by name. Unlike SeedClearDungeonQuestCommand, the target is one specific living creature —
// renamed with a distinguishing epithet so it reads as a named quarry rather than an ordinary
// monster — instead of the whole building's population. No-ops at any step where nothing eligible
// exists.
internal class SeedAssassinateQuestCommandHandler(
    IQueryHandler<
        GetCreatureIdsWithCreatureJobInLocationsQuery,
        IReadOnlyList<Guid>
    > getGiverCandidateIds,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<GetLocationIdsByCityIdQuery, IReadOnlyCollection<Guid>> getLocationIdsByCityId,
    IQueryHandler<GetBuildingsByWorldIdQuery, IReadOnlyCollection<Building>> getBuildingsByWorldId,
    IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>> getLocationsByIds,
    IQueryHandler<GetRoomsByBuildingIdsQuery, IReadOnlyCollection<Room>> getRoomsByBuildingIds,
    IQueryHandler<
        GetActiveKillCreatureObjectiveCreatureIdsQuery,
        IReadOnlySet<Guid>
    > getActiveKillCreatureObjectiveCreatureIds,
    IQueryHandler<
        GetLivingHostileCreatureIdsByLocationQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
    > getLivingHostileCreatureIdsByLocation,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    ICommandHandler<AddQuestCommand> addQuest
) : ICommandHandler<SeedAssassinateQuestCommand, bool>
{
    private const int GoldReward = 75;
    private const int GiverReputationReward = 20;

    // Authority/combat figures who'd plausibly put a price on a named creature's head, rather than
    // any employed resident of the city.
    private static readonly IReadOnlyList<Profession> EligibleGiverProfessions =
    [
        Profession.Guard,
        Profession.Knight,
        Profession.Politician,
        Profession.Mercenary,
    ];

    public async Task<bool> Handle(
        SeedAssassinateQuestCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var entranceLocation = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = command.LocationId },
            cancellationToken
        );
        if (entranceLocation?.CityId == null)
        {
            return false;
        }

        var giver = await FindGiver(entranceLocation.CityId.Value, cancellationToken);
        if (giver == null)
        {
            return false;
        }

        var candidate = await FindEligibleTarget(
            command,
            entranceLocation.StateId,
            cancellationToken
        );
        if (candidate == null)
        {
            return false;
        }

        var (building, targetLocationId, target) = candidate.Value;

        var newName = CreatureEpithetGenerator.ComposeName(target.Name, Random.Shared);
        await updateCreatures.Handle(
            new UpdateCreaturesCommand { CreatureIds = [target.Id], Name = newName },
            cancellationToken
        );

        var quest = new Quest
        {
            WorldId = command.WorldId,
            GiverId = giver.Id,
            Name = $"Silence {newName}",
            Description =
                $"{newName} has been terrorizing travelers from {building.Name}. Hunt them down.",
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
        var objective = new KillCreatureObjective
        {
            WorldId = command.WorldId,
            QuestId = quest.Id,
            Name = $"Kill {newName}",
            Description = $"Find and defeat {newName} in {building.Name}.",
            CreatureId = target.Id,
            LocationId = targetLocationId,
        };

        await addQuest.Handle(
            new AddQuestCommand { Quest = quest, Objectives = [objective] },
            cancellationToken
        );

        return true;
    }

    private async Task<Creature?> FindGiver(Guid cityId, CancellationToken cancellationToken)
    {
        var cityLocationIds = await getLocationIdsByCityId.Handle(
            new GetLocationIdsByCityIdQuery { CityId = cityId },
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

        var eligibleGivers = candidateGivers
            .Values.Where(creature =>
                CreatureTypes.Humanoid.Contains(creature.CreatureType)
                && creature.Profession is { } profession
                && EligibleGiverProfessions.Contains(profession)
            )
            .ToArray();

        return eligibleGivers.Length == 0
            ? null
            : eligibleGivers[Random.Shared.Next(eligibleGivers.Length)];
    }

    private async Task<(Building Building, Guid LocationId, Creature Target)?> FindEligibleTarget(
        SeedAssassinateQuestCommand command,
        Guid stateId,
        CancellationToken cancellationToken
    )
    {
        var buildings = await getBuildingsByWorldId.Handle(
            new GetBuildingsByWorldIdQuery { WorldId = command.WorldId },
            cancellationToken
        );
        var dungeonBuildings = buildings
            .Where(building => DungeonPopulator.SupportsDungeonType(building.BuildingType))
            .ToArray();
        if (dungeonBuildings.Length == 0)
        {
            return null;
        }

        var exteriorLocationsById = await getLocationsByIds.Handle(
            new GetLocationsByIdsQuery
            {
                Ids = dungeonBuildings.Select(building => building.ExteriorLocationId).ToArray(),
            },
            cancellationToken
        );

        var candidateBuildings = dungeonBuildings
            .Where(building =>
                exteriorLocationsById.TryGetValue(building.ExteriorLocationId, out var location)
                && location.StateId == stateId
            )
            .ToArray();
        if (candidateBuildings.Length == 0)
        {
            return null;
        }

        var candidateBuildingIds = candidateBuildings.Select(building => building.Id).ToArray();
        var rooms = await getRoomsByBuildingIds.Handle(
            new GetRoomsByBuildingIdsQuery { BuildingIds = candidateBuildingIds },
            cancellationToken
        );
        var bossRoomByBuildingId = rooms
            .Where(room => room.Role == RoomRole.BossChamber)
            .ToDictionary(room => room.BuildingId, room => room);
        if (bossRoomByBuildingId.Count == 0)
        {
            return null;
        }

        var bossLocationIds = bossRoomByBuildingId.Values.Select(room => room.LocationId).ToArray();
        var livingHostilesByLocation = await getLivingHostileCreatureIdsByLocation.Handle(
            new GetLivingHostileCreatureIdsByLocationQuery
            {
                WorldId = command.WorldId,
                LocationIds = bossLocationIds,
            },
            cancellationToken
        );
        var activeTargetCreatureIds = await getActiveKillCreatureObjectiveCreatureIds.Handle(
            new GetActiveKillCreatureObjectiveCreatureIdsQuery
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
            },
            cancellationToken
        );

        var buildingsById = candidateBuildings.ToDictionary(building => building.Id);
        var eligible = bossRoomByBuildingId
            .Select(pair => new
            {
                Building = buildingsById[pair.Key],
                Room = pair.Value,
                TargetIds = livingHostilesByLocation
                    .GetValueOrDefault(pair.Value.LocationId, [])
                    .Where(creatureId => !activeTargetCreatureIds.Contains(creatureId))
                    .ToArray(),
            })
            .Where(candidate => candidate.TargetIds.Length > 0)
            .ToArray();
        if (eligible.Length == 0)
        {
            return null;
        }

        var chosen = eligible[Random.Shared.Next(eligible.Length)];
        var targetId = chosen.TargetIds[Random.Shared.Next(chosen.TargetIds.Length)];
        var target = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = targetId },
            cancellationToken
        );

        return target == null ? null : (chosen.Building, chosen.Room.LocationId, target);
    }
}
