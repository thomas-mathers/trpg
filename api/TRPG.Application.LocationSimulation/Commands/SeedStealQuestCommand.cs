using System.Transactions;
using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Props.Queries;
using TRPG.Application.Quests.Commands;
using TRPG.Application.Quests.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

public class SeedStealQuestCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
}

// Finds someone at the seeding location who wants a few items quietly recovered from elsewhere in
// their city — from a pickpocket-able resident, a container, or a shop's stock. Repeatable the
// same way as the other seed commands: the giver is only excluded while the player already has an
// active give-items quest from them, not forever. The target pool always excludes the giver's own
// location — turning in requires the player to have left the theft's location first, which is what
// settles whether any witness survived to report it (see GetReportedStolenItemIdsQuery). No-ops at
// any step where nothing eligible exists.
internal class SeedStealQuestCommandHandler(
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<GetCityByIdQuery, City?> getCityById,
    IQueryHandler<GetLocationIdsByCityIdQuery, IReadOnlyCollection<Guid>> getLocationIdsByCityId,
    IQueryHandler<
        GetCreatureIdsWithCreatureJobInLocationsQuery,
        IReadOnlyList<Guid>
    > getCreatureIdsWithCreatureJobInLocations,
    IQueryHandler<GetContainerIdsByLocationsQuery, IReadOnlyList<Guid>> getContainerIdsByLocations,
    IQueryHandler<
        GetWorkstationIdsByLocationsQuery,
        IReadOnlyList<Guid>
    > getWorkstationIdsByLocations,
    IQueryHandler<GetPropByIdQuery, Prop?> getPropById,
    IQueryHandler<GetBuildingByLocationIdQuery, BuildingIdentity?> getBuildingByLocationId,
    IQueryHandler<
        GetActiveGiveItemsObjectiveRecipientIdsQuery,
        IReadOnlySet<Guid>
    > getActiveRecipientIds,
    ICommandHandler<AddItemsCommand> addItems,
    ICommandHandler<AddQuestCommand> addQuest,
    ILogger<SeedStealQuestCommandHandler> logger
) : ICommandHandler<SeedStealQuestCommand, bool>
{
    private const int GoldReward = 50;
    private const int GiverReputationReward = 14;
    private const int ItemCount = 3;

    // Order-aligned professions wouldn't plausibly hand out a theft contract; everyone else
    // (including a creature with no profession set) stays eligible.
    private static readonly IReadOnlyList<Profession> IneligibleGiverProfessions =
    [
        Profession.Guard,
        Profession.Knight,
        Profession.Cleric,
        Profession.Politician,
    ];

    public async Task<bool> Handle(
        SeedStealQuestCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var entranceLocation = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = command.LocationId },
            cancellationToken
        );
        if (entranceLocation?.CityId == null)
        {
            logger.LogInformation(
                "[steal-quest] seed location {LocationId} has no CityId",
                command.LocationId
            );
            return false;
        }

        var city = await getCityById.Handle(
            new GetCityByIdQuery { Id = entranceLocation.CityId.Value },
            cancellationToken
        );
        if (city == null)
        {
            logger.LogInformation(
                "[steal-quest] city {CityId} not found",
                entranceLocation.CityId.Value
            );
            return false;
        }

        var cityLocationIds = await getLocationIdsByCityId.Handle(
            new GetLocationIdsByCityIdQuery { CityId = city.Id },
            cancellationToken
        );

        var giver = await FindGiver(cityLocationIds, cancellationToken);
        if (giver == null)
        {
            logger.LogInformation("[steal-quest] no giver candidate in city {CityId}", city.Id);
            return false;
        }

        var activeRecipientIds = await getActiveRecipientIds.Handle(
            new GetActiveGiveItemsObjectiveRecipientIdsQuery
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
            },
            cancellationToken
        );
        if (activeRecipientIds.Contains(giver.Id))
        {
            logger.LogInformation(
                "[steal-quest] giver {GiverId} already has an active give-items quest",
                giver.Id
            );
            return false;
        }

        var targets = await FindEligibleTargets(giver, cityLocationIds, cancellationToken);
        if (targets == null)
        {
            logger.LogInformation(
                "[steal-quest] no eligible target pool in city {CityId}",
                city.Id
            );
            return false;
        }

        logger.LogInformation(
            "[steal-quest] seeding quest from giver {GiverId} with {TargetCount} targets: {Targets}",
            giver.Id,
            targets.Count,
            string.Join(", ", targets.Select(target => $"{target.Type}:{target.Id}"))
        );

        await CreateStealQuest(command, giver, city, targets, cancellationToken);

        return true;
    }

    private async Task<Creature?> FindGiver(
        IReadOnlyCollection<Guid> cityLocationIds,
        CancellationToken cancellationToken
    )
    {
        var candidateGiverIds = await getCreatureIdsWithCreatureJobInLocations.Handle(
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
                && (
                    creature.Profession is not { } profession
                    || !IneligibleGiverProfessions.Contains(profession)
                )
            )
            .ToArray();

        return eligibleGivers.Length == 0
            ? null
            : eligibleGivers[Random.Shared.Next(eligibleGivers.Length)];
    }

    private async Task<IReadOnlyList<ItemOwnerReference>?> FindEligibleTargets(
        Creature giver,
        IReadOnlyCollection<Guid> cityLocationIds,
        CancellationToken cancellationToken
    )
    {
        var otherLocationIds = cityLocationIds
            .Where(locationId => locationId != giver.LocationId)
            .ToArray();
        if (otherLocationIds.Length == 0)
        {
            logger.LogInformation("[steal-quest] no other locations in the city");
            return null;
        }

        var candidates = new List<ItemOwnerReference>();
        candidates.AddRange(
            await FindCreatureTargets(otherLocationIds, giver.Id, cancellationToken)
        );
        candidates.AddRange(
            (
                await getContainerIdsByLocations.Handle(
                    new GetContainerIdsByLocationsQuery { LocationIds = otherLocationIds },
                    cancellationToken
                )
            ).Select(id => new ItemOwnerReference(id, OwnerType.Container))
        );
        candidates.AddRange(
            (
                await getWorkstationIdsByLocations.Handle(
                    new GetWorkstationIdsByLocationsQuery { LocationIds = otherLocationIds },
                    cancellationToken
                )
            ).Select(id => new ItemOwnerReference(id, OwnerType.Workstation))
        );

        if (candidates.Count < ItemCount)
        {
            logger.LogInformation(
                "[steal-quest] only {CandidateCount} candidate targets found across {LocationCount} other locations",
                candidates.Count,
                otherLocationIds.Length
            );
            return null;
        }

        return candidates.OrderBy(_ => Random.Shared.Next()).Take(ItemCount).ToArray();
    }

    private async Task<IReadOnlyList<ItemOwnerReference>> FindCreatureTargets(
        IReadOnlyCollection<Guid> otherLocationIds,
        Guid giverId,
        CancellationToken cancellationToken
    )
    {
        var candidateIds = await getCreatureIdsWithCreatureJobInLocations.Handle(
            new GetCreatureIdsWithCreatureJobInLocationsQuery { LocationIds = otherLocationIds },
            cancellationToken
        );
        var candidates = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = candidateIds },
            cancellationToken
        );

        return candidates
            .Values.Where(creature =>
                CreatureTypes.Humanoid.Contains(creature.CreatureType) && creature.Id != giverId
            )
            .Select(creature => new ItemOwnerReference(creature.Id, OwnerType.Creature))
            .ToArray();
    }

    // A pickpocket target's item is a concrete personal valuable; a container/workstation
    // target's is a concrete stashed valuable. Combined possessively with who/where it's from,
    // the result reads as a real item name ("Lucan Ashvale's Signet Ring") that also happens to
    // tell the quest journal's per-item breakdown exactly what to search for.
    private static readonly string[] PersonalValuables =
    [
        "Signet Ring",
        "Pocket Watch",
        "Silver Locket",
        "Engraved Hairpin",
        "Ivory Comb",
        "Coin Purse",
    ];

    private static readonly string[] StashedValuables =
    [
        "Strongbox",
        "Ledger",
        "Jewelry Case",
        "Silver Candlestick",
        "Antique Vase",
        "Sealed Letter",
    ];

    private async Task<IReadOnlyDictionary<Guid, string>> ResolveClues(
        IReadOnlyList<ItemOwnerReference> targets,
        CancellationToken cancellationToken
    )
    {
        var clues = new Dictionary<Guid, string>();

        var creatureTargetIds = targets
            .Where(target => target.Type == OwnerType.Creature)
            .Select(target => target.Id)
            .ToArray();
        if (creatureTargetIds.Length > 0)
        {
            var creaturesById = await getCreaturesByIds.Handle(
                new GetCreaturesByIdsQuery { Ids = creatureTargetIds },
                cancellationToken
            );
            foreach (var targetId in creatureTargetIds)
            {
                var valuable = PersonalValuables[Random.Shared.Next(PersonalValuables.Length)];
                clues[targetId] = $"{creaturesById[targetId].Name}'s {valuable}";
            }
        }

        foreach (var target in targets.Where(target => target.Type != OwnerType.Creature))
        {
            clues[target.Id] = await ResolvePropClue(target.Id, cancellationToken);
        }

        return clues;
    }

    private async Task<string> ResolvePropClue(Guid propId, CancellationToken cancellationToken)
    {
        var valuable = StashedValuables[Random.Shared.Next(StashedValuables.Length)];

        var prop = await getPropById.Handle(
            new GetPropByIdQuery { Id = propId },
            cancellationToken
        );
        if (prop == null)
        {
            return $"Unclaimed {valuable}";
        }

        var building = await getBuildingByLocationId.Handle(
            new GetBuildingByLocationIdQuery { LocationId = prop.LocationId },
            cancellationToken
        );

        return building == null ? $"Unclaimed {valuable}" : $"{building.Name}'s {valuable}";
    }

    private async Task CreateStealQuest(
        SeedStealQuestCommand command,
        Creature giver,
        City city,
        IReadOnlyList<ItemOwnerReference> targets,
        CancellationToken cancellationToken
    )
    {
        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var clueByTargetId = await ResolveClues(targets, cancellationToken);
        var items = targets
            .Select(target => new Item
            {
                WorldId = command.WorldId,
                Name = clueByTargetId[target.Id],
                Description = $"{giver.Name} wants this recovered without a scene.",
                Quantity = 1,
                Ownership = new ItemOwnership { OwnerId = target.Id, OwnerType = target.Type },
            })
            .ToArray();
        await addItems.Handle(new AddItemsCommand { Items = items }, cancellationToken);

        var isPersonal = Random.Shared.Next(2) == 0;
        var quest = new Quest
        {
            WorldId = command.WorldId,
            GiverId = giver.Id,
            Name = isPersonal ? "A Personal Matter" : "A Quiet Job",
            Description = isPersonal
                ? $"{giver.Name} says someone in {city.Name} is holding something of theirs, and wants it back quietly — no questions asked."
                : $"{giver.Name} needs a few things acquired around {city.Name}. How you get them is your business.",
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
        var objectives = items
            .GroupBy(item => item.Name)
            .Select(group => new GiveItemsObjective
            {
                WorldId = command.WorldId,
                QuestId = quest.Id,
                Name =
                    group.Count() > 1
                        ? $"Recover {group.Count()}x {group.Key}"
                        : $"Recover {group.Key}",
                Description =
                    group.Count() > 1
                        ? $"Quietly recover {group.Count()} of {group.Key} and bring them to {giver.Name}."
                        : $"Quietly recover {group.Key} and bring it to {giver.Name}.",
                ItemIds = group.Select(item => item.Id).ToList(),
                RecipientId = giver.Id,
                RequiredAmount = group.Count(),
            })
            .ToArray();

        await addQuest.Handle(
            new AddQuestCommand { Quest = quest, Objectives = objectives },
            cancellationToken
        );

        transaction.Complete();
    }
}
