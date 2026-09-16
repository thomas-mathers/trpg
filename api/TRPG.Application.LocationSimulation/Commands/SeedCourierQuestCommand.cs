using System.Transactions;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Quests.Commands;
using TRPG.Application.Quests.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

public class SeedCourierQuestCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
}

// Finds someone at the seeding location who wants a package carried to another resident
// elsewhere in the city. Repeatable the same way as SeedClearDungeonQuestCommand: a recipient is
// only excluded while the player already has an active delivery to them, not forever.
internal class SeedCourierQuestCommandHandler(
    IQueryHandler<
        GetCreatureIdsWithCreatureJobInLocationsQuery,
        IReadOnlyList<Guid>
    > getCreatureIdsWithCreatureJobInLocations,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById,
    IQueryHandler<GetLocationIdsByCityIdQuery, IReadOnlyCollection<Guid>> getLocationIdsByCityId,
    IQueryHandler<
        GetActiveDeliverItemObjectiveRecipientIdsQuery,
        IReadOnlySet<Guid>
    > getActiveRecipientIds,
    ICommandHandler<AddItemsCommand> addItems,
    ICommandHandler<AddQuestCommand> addQuest
) : ICommandHandler<SeedCourierQuestCommand, bool>
{
    private const int GoldReward = 30;
    private const int GiverReputationReward = 8;

    // Professions that would plausibly need something delivered, rather than any employed
    // resident of the city.
    private static readonly IReadOnlyList<Profession> EligibleGiverProfessions =
    [
        Profession.Merchant,
        Profession.Innkeeper,
        Profession.Baker,
        Profession.Tailor,
        Profession.Jeweler,
        Profession.Blacksmith,
    ];

    public async Task<bool> Handle(
        SeedCourierQuestCommand command,
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

        var cityLocationIds = await getLocationIdsByCityId.Handle(
            new GetLocationIdsByCityIdQuery { CityId = entranceLocation.CityId.Value },
            cancellationToken
        );

        var giver = await FindGiver(cityLocationIds, cancellationToken);
        if (giver == null)
        {
            return false;
        }

        var recipient = await FindRecipient(command, giver, cityLocationIds, cancellationToken);
        if (recipient == null)
        {
            return false;
        }

        await CreateDelivery(command, giver, recipient, cancellationToken);

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
                && creature.Profession is { } profession
                && EligibleGiverProfessions.Contains(profession)
            )
            .ToArray();

        return eligibleGivers.Length == 0
            ? null
            : eligibleGivers[Random.Shared.Next(eligibleGivers.Length)];
    }

    private async Task<Creature?> FindRecipient(
        SeedCourierQuestCommand command,
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
            return null;
        }

        var candidateRecipientIds = await getCreatureIdsWithCreatureJobInLocations.Handle(
            new GetCreatureIdsWithCreatureJobInLocationsQuery { LocationIds = otherLocationIds },
            cancellationToken
        );
        var candidateRecipients = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = candidateRecipientIds },
            cancellationToken
        );

        var activeRecipientIds = await getActiveRecipientIds.Handle(
            new GetActiveDeliverItemObjectiveRecipientIdsQuery
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
            },
            cancellationToken
        );

        var eligibleRecipients = candidateRecipients
            .Values.Where(creature =>
                CreatureTypes.Humanoid.Contains(creature.CreatureType)
                && creature.Id != giver.Id
                && !activeRecipientIds.Contains(creature.Id)
            )
            .ToArray();

        return eligibleRecipients.Length == 0
            ? null
            : eligibleRecipients[Random.Shared.Next(eligibleRecipients.Length)];
    }

    private async Task CreateDelivery(
        SeedCourierQuestCommand command,
        Creature giver,
        Creature recipient,
        CancellationToken cancellationToken
    )
    {
        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var package = new Item
        {
            WorldId = command.WorldId,
            Name = $"Package for {recipient.Name}",
            Description = $"A sealed package addressed to {recipient.Name}.",
            Quantity = 1,
            Ownership = new ItemOwnership { OwnerId = giver.Id, OwnerType = OwnerType.Creature },
        };
        await addItems.Handle(new AddItemsCommand { Items = [package] }, cancellationToken);

        var quest = new Quest
        {
            WorldId = command.WorldId,
            GiverId = giver.Id,
            Name = $"A Delivery for {recipient.Name}",
            Description = $"{giver.Name} needs a package delivered to {recipient.Name}.",
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
        var objective = new DeliverItemObjective
        {
            WorldId = command.WorldId,
            QuestId = quest.Id,
            Name = $"Deliver the package to {recipient.Name}",
            Description = $"Deliver the sealed package to {recipient.Name}.",
            ItemId = package.Id,
            RecipientId = recipient.Id,
        };
        await addQuest.Handle(
            new AddQuestCommand { Quest = quest, Objectives = [objective] },
            cancellationToken
        );

        transaction.Complete();
    }
}
