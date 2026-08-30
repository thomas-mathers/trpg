using Microsoft.Extensions.Logging;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Quests.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Commands;

public class CleanUpAbandonedCorpsesCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
}

internal class CleanUpAbandonedCorpsesCommandHandler(
    IQueryHandler<
        GetCreaturesAtLocationQuery,
        IReadOnlyCollection<CreatureResult>
    > getCreaturesAtLocation,
    IQueryHandler<GetActiveQuestItemIdsQuery, IReadOnlyCollection<Guid>> getActiveQuestItemIds,
    IQueryHandler<
        GetCreatureIdsHoldingItemsQuery,
        IReadOnlyCollection<Guid>
    > getCreatureIdsHoldingItems,
    IQueryHandler<
        GetInventoryItemsByOwnersQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Item>>
    > getInventoryItemsByOwners,
    ICommandHandler<DeleteCreaturesCommand> deleteCreatures,
    ILogger<CleanUpAbandonedCorpsesCommandHandler> logger
) : ICommandHandler<CleanUpAbandonedCorpsesCommand>
{
    public async Task Handle(
        CleanUpAbandonedCorpsesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var nearby = await getCreaturesAtLocation.Handle(
            new GetCreaturesAtLocationQuery
            {
                WorldId = command.WorldId,
                LocationId = command.LocationId,
            },
            cancellationToken
        );

        var deadCreatureIds = nearby
            .Where(creature => creature.State == CreatureState.Dead)
            .Select(creature => creature.Id)
            .ToArray();

        if (deadCreatureIds.Length == 0)
        {
            return;
        }

        var questItemIds = await getActiveQuestItemIds.Handle(
            new GetActiveQuestItemIdsQuery { PlayerId = command.PlayerId },
            cancellationToken
        );
        var questItemOwnerIds = await getCreatureIdsHoldingItems.Handle(
            new GetCreatureIdsHoldingItemsQuery { ItemIds = questItemIds },
            cancellationToken
        );
        var playerCorpseIds = nearby
            .Where(creature => creature.PlayerCorpseOwnerId != null)
            .Select(creature => creature.Id)
            .ToArray();
        var itemsByPlayerCorpse = await getInventoryItemsByOwners.Handle(
            new GetInventoryItemsByOwnersQuery { CreatureIds = playerCorpseIds },
            cancellationToken
        );
        var unlootedPlayerCorpseIds = playerCorpseIds.Where(itemsByPlayerCorpse.ContainsKey);
        var removableCreatureIds = deadCreatureIds
            .Except(questItemOwnerIds)
            .Except(unlootedPlayerCorpseIds)
            .ToArray();

        if (removableCreatureIds.Length == 0)
        {
            return;
        }

        logger.LogInformation(
            "[scene] deleting {Count} dead creature(s) left behind: {CreatureIds}",
            removableCreatureIds.Length,
            string.Join(", ", removableCreatureIds)
        );

        await deleteCreatures.Handle(
            new DeleteCreaturesCommand { CreatureIds = removableCreatureIds },
            cancellationToken
        );
    }
}
