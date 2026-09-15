using System.Transactions;
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
    IQueryHandler<
        GetCreatureIdsWithCreatureJobInLocationQuery,
        IReadOnlyList<Guid>
    > getGiverCandidateIds,
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
    IQueryHandler<
        GetActiveGiveItemsObjectiveRecipientIdsQuery,
        IReadOnlySet<Guid>
    > getActiveRecipientIds,
    ICommandHandler<AddItemsCommand> addItems,
    ICommandHandler<AddQuestCommand> addQuest
) : ICommandHandler<SeedStealQuestCommand, bool>
{
    private const int GoldReward = 50;
    private const int GiverReputationReward = 14;
    private const int ItemCount = 3;

    public async Task<bool> Handle(
        SeedStealQuestCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var giver = await FindGiver(command, cancellationToken);
        if (giver == null)
        {
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
            return false;
        }

        var giverLocation = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = command.LocationId },
            cancellationToken
        );
        if (giverLocation?.CityId == null)
        {
            return false;
        }

        var city = await getCityById.Handle(
            new GetCityByIdQuery { Id = giverLocation.CityId.Value },
            cancellationToken
        );
        if (city == null)
        {
            return false;
        }

        var targets = await FindEligibleTargets(command, giver.Id, city.Id, cancellationToken);
        if (targets == null)
        {
            return false;
        }

        await CreateStealQuest(command, giver, city, targets, cancellationToken);

        return true;
    }

    private async Task<Creature?> FindGiver(
        SeedStealQuestCommand command,
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

        return candidateGivers
            .Values.Where(creature => CreatureTypes.Humanoid.Contains(creature.CreatureType))
            .FirstOrDefault();
    }

    private async Task<IReadOnlyList<ItemOwnerReference>?> FindEligibleTargets(
        SeedStealQuestCommand command,
        Guid giverId,
        Guid cityId,
        CancellationToken cancellationToken
    )
    {
        var cityLocationIds = await getLocationIdsByCityId.Handle(
            new GetLocationIdsByCityIdQuery { CityId = cityId },
            cancellationToken
        );
        var otherLocationIds = cityLocationIds
            .Where(locationId => locationId != command.LocationId)
            .ToArray();
        if (otherLocationIds.Length == 0)
        {
            return null;
        }

        var candidates = new List<ItemOwnerReference>();
        candidates.AddRange(
            await FindCreatureTargets(otherLocationIds, giverId, cancellationToken)
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

        var items = targets
            .Select(target => new Item
            {
                WorldId = command.WorldId,
                Name = "Recovered Item",
                Description = $"One of several items {giver.Name} wants recovered without a scene.",
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
        var objective = new GiveItemsObjective
        {
            WorldId = command.WorldId,
            QuestId = quest.Id,
            Name = "Recover the goods",
            Description =
                $"Quietly recover {items.Length} items from around {city.Name} and bring them to {giver.Name}.",
            ItemIds = items.Select(item => item.Id).ToList(),
            RecipientId = giver.Id,
            RequiredAmount = items.Length,
        };

        await addQuest.Handle(
            new AddQuestCommand { Quest = quest, Objectives = [objective] },
            cancellationToken
        );

        transaction.Complete();
    }
}
