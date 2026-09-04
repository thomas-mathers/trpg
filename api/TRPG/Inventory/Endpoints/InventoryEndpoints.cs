using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Inventory.Results;
using TRPG.Application.Props.Queries;
using TRPG.Application.Quests.Queries;
using TRPG.Creatures.Mappers;
using TRPG.Domain.Models;
using TRPG.GameSessions.Hubs;
using TRPG.Inventory.Mappers;
using TRPG.Inventory.Requests;
using TRPG.Inventory.Responses;

namespace TRPG.Inventory.Endpoints;

internal static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this WebApplication app)
    {
        app.MapPost("/players/{playerId:guid}/inventory-transfers", InventoryTransfer)
            .WithName("TransferInventory")
            .Produces<InventoryTransferResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);
        app.MapPost(
                "/players/{playerId:guid}/inventory-items/{itemId:guid}/drop",
                DropInventoryItem
            )
            .WithName("DropInventoryItem")
            .ProducesProblem(StatusCodes.Status400BadRequest);
        app.MapPost("/players/{playerId:guid}/theft-detection-chance", GetTheftDetectionChance)
            .WithName("GetTheftDetectionChance");
        app.MapGet("/players/{playerId:guid}/trades/{workstationId:guid}", GetTrade)
            .WithName("GetTrade");
        app.MapPost("/players/{playerId:guid}/trades/{workstationId:guid}/proposal", ProposeTrade)
            .WithName("ProposeTrade");
        app.MapPost("/players/{playerId:guid}/trades/{workstationId:guid}/complete", CompleteTrade)
            .WithName("CompleteTrade");
        app.MapGet("/sessions/{sessionId:guid}/items/{itemId:guid}", GetItemById)
            .WithName("GetSessionItem");
        app.MapGet("/containers/{containerId:guid}/inventory", GetContainerInventory)
            .WithName("GetContainerInventory");
        app.MapGet("/workstations/{workstationId:guid}/inventory", GetWorkstationInventory)
            .WithName("GetWorkstationInventory");
    }

    private static async Task<Ok<InventorySummary>> GetContainerInventory(
        Guid containerId,
        [FromServices] IQueryHandler<GetInventoryByOwnerQuery, InventoryResult> getInventoryByOwner,
        CancellationToken cancellationToken
    )
    {
        var snapshot = await getInventoryByOwner.Handle(
            new GetInventoryByOwnerQuery
            {
                Owner = new ItemOwnerReference(containerId, OwnerType.Container),
            },
            cancellationToken
        );

        return TypedResults.Ok(snapshot.ToSummary([]));
    }

    private static async Task<Ok<InventorySummary>> GetWorkstationInventory(
        Guid workstationId,
        [FromServices] IQueryHandler<GetInventoryByOwnerQuery, InventoryResult> getInventoryByOwner,
        CancellationToken cancellationToken
    )
    {
        var snapshot = await getInventoryByOwner.Handle(
            new GetInventoryByOwnerQuery
            {
                Owner = new ItemOwnerReference(workstationId, OwnerType.Workstation),
            },
            cancellationToken
        );

        return TypedResults.Ok(snapshot.ToSummary([]));
    }

    private static async Task<
        Results<NotFound, ProblemHttpResult, Ok<InventoryTransferResponse>>
    > InventoryTransfer(
        Guid playerId,
        InventoryTransferRequest request,
        [FromServices] IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
        [FromServices] IQueryHandler<GetPropByIdQuery, Prop?> getPropById,
        [FromServices] IQueryHandler<GetActiveEncounterQuery, Encounter?> getActiveEncounter,
        [FromServices]
            IQueryHandler<
            GetInventoryItemsByOwnerQuery,
            IReadOnlyList<Item>
        > getInventoryItemsByOwner,
        [FromServices]
            IQueryHandler<GetItemsByIdsForOwnerQuery, IReadOnlyList<Item>> getItemsByIdsForOwner,
        [FromServices] ICommandHandler<ReceivePlayerInventoryCommand> receiveInventory,
        [FromServices] ICommandHandler<AttemptTheftCommand, TheftAttemptResult> attemptTheft,
        [FromServices] ICommandHandler<TransferPlayerInventoryCommand> transferInventory,
        GameClientEventDispatcher eventDispatcher,
        CancellationToken cancellationToken
    )
    {
        var activeEncounter = await getActiveEncounter.Handle(
            new GetActiveEncounterQuery { PlayerId = playerId },
            cancellationToken
        );
        if (activeEncounter != null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Encounter active",
                detail: "Resolve the active encounter before transferring inventory."
            );
        }

        var from = await ResolveOwnerLocation(
            request.From,
            getCreatureById,
            getPropById,
            cancellationToken
        );
        if (from == null)
        {
            return TypedResults.NotFound();
        }

        var to = await ResolveOwnerLocation(
            request.To,
            getCreatureById,
            getPropById,
            cancellationToken
        );
        if (to == null)
        {
            return TypedResults.NotFound();
        }

        if (from.Value.LocationId != to.Value.LocationId)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Owners are not nearby",
                detail: "Inventory can only be transferred between owners at the same location."
            );
        }

        if (from.Value.WorldId != to.Value.WorldId)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Owners are in different worlds"
            );
        }

        var response = new InventoryTransferResponse(null);
        if (request.From.Id == playerId && request.From.Type == OwnerType.Creature)
        {
            await transferInventory.Handle(
                new TransferPlayerInventoryCommand
                {
                    To = new ItemOwnerReference(request.To.Id, request.To.Type),
                    Items = request.Items,
                    PlayerId = playerId,
                },
                cancellationToken
            );
        }
        else if (request.To.Id == playerId && request.To.Type == OwnerType.Creature)
        {
            var theftAttempt = await attemptTheft.Handle(
                new AttemptTheftCommand
                {
                    From = new ItemOwnerReference(request.From.Id, request.From.Type),
                    Items = request.Items,
                    PlayerId = playerId,
                    WorldId = from.Value.WorldId,
                },
                cancellationToken
            );
            if (theftAttempt.Outcome == TheftAttemptOutcome.NotTheft)
            {
                var hasCapacity = await DestinationHasCapacity(
                    playerId,
                    new ItemOwnerReference(request.From.Id, request.From.Type),
                    request.Items,
                    getCreatureById,
                    getInventoryItemsByOwner,
                    getItemsByIdsForOwner,
                    cancellationToken
                );
                if (!hasCapacity)
                {
                    return TypedResults.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Carrying capacity exceeded",
                        detail: "You cannot carry any more weight."
                    );
                }

                await receiveInventory.Handle(
                    new ReceivePlayerInventoryCommand
                    {
                        From = new ItemOwnerReference(request.From.Id, request.From.Type),
                        Items = request.Items,
                        PlayerId = playerId,
                        WorldId = from.Value.WorldId,
                    },
                    cancellationToken
                );
            }
            response = new InventoryTransferResponse(theftAttempt.EncounterId);
        }
        else
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Inventory transfers must involve the player"
            );
        }

        await eventDispatcher.FlushAsync(from.Value.WorldId, cancellationToken);

        return TypedResults.Ok(response);
    }

    private static async Task<NoContent> DropInventoryItem(
        Guid playerId,
        Guid itemId,
        DropInventoryItemRequest request,
        [FromServices] ICommandHandler<DropInventoryItemCommand> dropInventoryItem,
        CancellationToken cancellationToken
    )
    {
        await dropInventoryItem.Handle(
            new DropInventoryItemCommand
            {
                PlayerId = playerId,
                ItemId = itemId,
                Quantity = request.Quantity,
            },
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    private static async Task<bool> DestinationHasCapacity(
        Guid creatureId,
        ItemOwnerReference from,
        IReadOnlyList<ItemSelection> incomingSelections,
        IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
        IQueryHandler<GetInventoryItemsByOwnerQuery, IReadOnlyList<Item>> getInventoryItemsByOwner,
        IQueryHandler<GetItemsByIdsForOwnerQuery, IReadOnlyList<Item>> getItemsByIdsForOwner,
        CancellationToken cancellationToken
    )
    {
        var creature = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = creatureId },
            cancellationToken
        );
        if (creature is not { State: not CreatureState.Dead })
        {
            return true;
        }

        var currentItems = await getInventoryItemsByOwner.Handle(
            new GetInventoryItemsByOwnerQuery
            {
                Owner = new ItemOwnerReference(creatureId, OwnerType.Creature),
            },
            cancellationToken
        );
        var currentWeight = currentItems.Sum(item => item.Weight * item.Quantity);

        var incomingItems = await getItemsByIdsForOwner.Handle(
            new GetItemsByIdsForOwnerQuery
            {
                ItemIds = incomingSelections.Select(selection => selection.ItemId).ToArray(),
                OwnerId = from.Id,
                OwnerType = from.Type,
            },
            cancellationToken
        );
        var quantityByItemId = incomingSelections.ToDictionary(
            selection => selection.ItemId,
            selection => selection.Quantity
        );
        var incomingWeight = incomingItems.Sum(item =>
            item.Weight * quantityByItemId.GetValueOrDefault(item.Id, 0)
        );

        return currentWeight + incomingWeight <= creature.CarryingCapacity;
    }

    private static async Task<(Guid LocationId, Guid WorldId)?> ResolveOwnerLocation(
        OwnerReferenceRequest owner,
        IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
        IQueryHandler<GetPropByIdQuery, Prop?> getPropById,
        CancellationToken cancellationToken
    )
    {
        if (owner.Type == OwnerType.Creature)
        {
            var creature = await getCreatureById.Handle(
                new GetCreatureByIdQuery { Id = owner.Id },
                cancellationToken
            );
            return creature == null ? null : (creature.LocationId, creature.WorldId);
        }

        var prop = await getPropById.Handle(
            new GetPropByIdQuery { Id = owner.Id },
            cancellationToken
        );
        return prop == null ? null : (prop.LocationId, prop.WorldId);
    }

    private static async Task<
        Results<NotFound, Ok<TheftDetectionChanceResponse>>
    > GetTheftDetectionChance(
        Guid playerId,
        TheftDetectionChanceRequest request,
        [FromServices] IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
        [FromServices] IQueryHandler<GetPropByIdQuery, Prop?> getPropById,
        [FromServices] IQueryHandler<GetTheftDetectionChanceQuery, float?> getTheftDetectionChance,
        CancellationToken cancellationToken
    )
    {
        var from = await ResolveOwnerLocation(
            request.From,
            getCreatureById,
            getPropById,
            cancellationToken
        );
        if (from == null)
        {
            return TypedResults.NotFound();
        }

        var chance = await getTheftDetectionChance.Handle(
            new GetTheftDetectionChanceQuery
            {
                PlayerId = playerId,
                WorldId = from.Value.WorldId,
                From = new ItemOwnerReference(request.From.Id, request.From.Type),
                Items = request.Items,
            },
            cancellationToken
        );

        return TypedResults.Ok(new TheftDetectionChanceResponse(chance));
    }

    private static async Task<Results<NotFound, Ok<ItemDetail>>> GetItemById(
        Guid sessionId,
        Guid itemId,
        [FromServices] IQueryHandler<GetGameSessionQuery, GameSession> getGameSession,
        [FromServices] IQueryHandler<GetItemByIdQuery, Item?> getItemById,
        CancellationToken cancellationToken
    )
    {
        var session = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = sessionId },
            cancellationToken
        );
        var item = await getItemById.Handle(
            new GetItemByIdQuery { ItemId = itemId, WorldId = session.WorldId },
            cancellationToken
        );

        return item is null ? TypedResults.NotFound() : TypedResults.Ok(item.ToDetail());
    }

    private static async Task<Ok<TradeSnapshot>> GetTrade(
        Guid playerId,
        Guid workstationId,
        [FromServices] IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
        [FromServices] IQueryHandler<GetTradeQuery, TradeResult> getTrade,
        [FromServices]
            IQueryHandler<
            GetActiveQuestItemIdsQuery,
            IReadOnlyCollection<Guid>
        > getActiveQuestItemIds,
        CancellationToken cancellationToken
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = playerId },
            cancellationToken
        );
        var trade = await getTrade.Handle(
            new GetTradeQuery { PlayerId = playerId, WorkstationId = workstationId },
            cancellationToken
        );
        var questItemIds = await getActiveQuestItemIds.Handle(
            new GetActiveQuestItemIdsQuery { PlayerId = playerId },
            cancellationToken
        );

        return TypedResults.Ok(
            new TradeSnapshot(
                trade.PlayerInventory.ToSummary(questItemIds, player?.CarryingCapacity),
                trade.ShopInventory.ToSummary([])
            )
        );
    }

    private static async Task<Results<BadRequest, Ok<TradeProposalResponse>>> ProposeTrade(
        Guid playerId,
        Guid workstationId,
        TradeRequest request,
        [FromServices] ICommandHandler<ProposeTradeCommand, TradeOutcome> proposeTrade,
        [FromServices] IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
        CancellationToken cancellationToken
    )
    {
        await EnsureCreatureExists(playerId, getCreatureById, cancellationToken);

        var outcome = await proposeTrade.Handle(
            new ProposeTradeCommand
            {
                PlayerId = playerId,
                WorkstationId = workstationId,
                PlayerOffer = request.PlayerOffer,
                ShopOffer = request.ShopOffer,
            },
            cancellationToken
        );
        return TypedResults.Ok(new TradeProposalResponse(outcome.ToStatus()));
    }

    private static async Task<Results<ProblemHttpResult, NoContent>> CompleteTrade(
        Guid playerId,
        Guid workstationId,
        Guid worldId,
        TradeRequest request,
        [FromServices] ICommandHandler<CompleteTradeCommand> completeTrade,
        [FromServices] IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
        [FromServices]
            IQueryHandler<
            GetInventoryItemsByOwnerQuery,
            IReadOnlyList<Item>
        > getInventoryItemsByOwner,
        [FromServices]
            IQueryHandler<GetItemsByIdsForOwnerQuery, IReadOnlyList<Item>> getItemsByIdsForOwner,
        CancellationToken cancellationToken
    )
    {
        await EnsureCreatureExists(playerId, getCreatureById, cancellationToken);

        var hasCapacity = await DestinationHasCapacity(
            playerId,
            new ItemOwnerReference(workstationId, OwnerType.Workstation),
            request.ShopOffer,
            getCreatureById,
            getInventoryItemsByOwner,
            getItemsByIdsForOwner,
            cancellationToken
        );
        if (!hasCapacity)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Carrying capacity exceeded",
                detail: "You cannot carry any more weight."
            );
        }

        await completeTrade.Handle(
            new CompleteTradeCommand
            {
                PlayerId = playerId,
                WorldId = worldId,
                WorkstationId = workstationId,
                PlayerOffer = request.PlayerOffer,
                ShopOffer = request.ShopOffer,
            },
            cancellationToken
        );
        return TypedResults.NoContent();
    }

    private static async Task EnsureCreatureExists(
        Guid creatureId,
        IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
        CancellationToken cancellationToken
    )
    {
        var creature = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = creatureId },
            cancellationToken
        );
        if (creature == null)
        {
            throw new EntityNotFoundException(nameof(Creature), creatureId);
        }
    }
}
