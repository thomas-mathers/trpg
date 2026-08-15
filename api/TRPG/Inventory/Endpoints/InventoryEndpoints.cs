using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Handling;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Quests.Queries;
using TRPG.Application.Trading;
using TRPG.Application.Trading.Commands;
using TRPG.Application.Trading.Queries;
using TRPG.Contracts.Inventory.Requests;
using TRPG.Contracts.Inventory.Responses;
using TRPG.Contracts.Trading.Requests;
using TRPG.Contracts.Trading.Responses;
using TRPG.Creatures.Endpoints;
using TRPG.Data;
using TRPG.Data.Models;
using TRPG.GameSessions.Hubs;

namespace TRPG.Inventory.Endpoints;

internal static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this WebApplication app)
    {
        app.MapPost("/players/{playerId:guid}/inventory-transfers", InventoryTransfer)
            .WithName("TransferInventory")
            .ProducesProblem(StatusCodes.Status400BadRequest);
        app.MapGet("/players/{playerId:guid}/trades/{workstationId:guid}", GetTrade)
            .WithName("GetTrade");
        app.MapPost("/players/{playerId:guid}/trades/{workstationId:guid}/proposal", ProposeTrade)
            .WithName("ProposeTrade");
        app.MapPost("/players/{playerId:guid}/trades/{workstationId:guid}/complete", CompleteTrade)
            .WithName("CompleteTrade");
        app.MapGet("/sessions/{sessionId:guid}/items/{itemId:guid}", GetItemById)
            .WithName("GetSessionItem");
    }

    private static async Task<Results<NotFound, ProblemHttpResult, NoContent>> InventoryTransfer(
        Guid playerId,
        InventoryTransferRequest request,
        [FromServices] IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
        [FromServices] ICommandHandler<ReceivePlayerInventoryCommand> receiveInventory,
        [FromServices] ICommandHandler<TransferPlayerInventoryCommand> transferInventory,
        GameClientEventDispatcher eventDispatcher,
        CancellationToken cancellationToken
    )
    {
        var fromCreature = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = request.FromId },
            cancellationToken
        );
        if (fromCreature == null)
        {
            return TypedResults.NotFound();
        }

        var toCreature = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = request.ToId },
            cancellationToken
        );
        if (toCreature == null)
        {
            return TypedResults.NotFound();
        }

        if (fromCreature.LocationId != toCreature.LocationId)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Creatures are not nearby",
                detail: "Inventory can only be transferred between creatures at the same location."
            );
        }

        if (fromCreature.WorldId != toCreature.WorldId)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Creatures are in different worlds"
            );
        }

        if (request.FromId == playerId)
        {
            await transferInventory.Handle(
                new TransferPlayerInventoryCommand
                {
                    To = new ItemOwnerReference(request.ToId, OwnerType.Creature),
                    Items = request.Items,
                    PlayerId = playerId,
                },
                cancellationToken
            );
        }
        else if (request.ToId == playerId)
        {
            await receiveInventory.Handle(
                new ReceivePlayerInventoryCommand
                {
                    From = new ItemOwnerReference(request.FromId, OwnerType.Creature),
                    Items = request.Items,
                    PlayerId = playerId,
                    WorldId = fromCreature.WorldId,
                },
                cancellationToken
            );
        }
        else
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Inventory transfers must involve the player"
            );
        }

        await eventDispatcher.FlushAsync(fromCreature.WorldId, cancellationToken);

        return TypedResults.NoContent();
    }

    private static async Task<Results<NotFound, Ok<ItemDetail>>> GetItemById(
        Guid sessionId,
        Guid itemId,
        [FromServices] IQueryHandler<GetGameSessionQuery, GameSession> getGameSession,
        TrpgDbContext context,
        CancellationToken cancellationToken
    )
    {
        var session = await getGameSession.Handle(
            new GetGameSessionQuery { SessionId = sessionId },
            cancellationToken
        );
        var item = await context
            .Items.AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.Id == itemId && item.WorldId == session.WorldId,
                cancellationToken
            );

        return item is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(CreatureEndpoints.ToItemDetail(item));
    }

    private static async Task<Ok<TradeSnapshot>> GetTrade(
        Guid playerId,
        Guid workstationId,
        GetTradeQueryHandler getTrade,
        [FromServices]
            IQueryHandler<
            GetActiveQuestItemIdsQuery,
            IReadOnlyCollection<Guid>
        > getActiveQuestItemIds,
        CancellationToken cancellationToken
    )
    {
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
                ToSummary(trade.PlayerInventory, questItemIds),
                ToSummary(trade.ShopInventory, [])
            )
        );
    }

    private static async Task<Results<BadRequest, Ok<TradeProposalResponse>>> ProposeTrade(
        Guid playerId,
        Guid workstationId,
        TradeRequest request,
        ProposeTradeCommandHandler proposeTrade,
        CancellationToken cancellationToken
    )
    {
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
        return TypedResults.Ok(
            new TradeProposalResponse(
                outcome == TradeOutcome.Accepted
                    ? TradeProposalStatus.Accepted
                    : TradeProposalStatus.Rejected
            )
        );
    }

    private static async Task<NoContent> CompleteTrade(
        Guid playerId,
        Guid workstationId,
        Guid worldId,
        TradeRequest request,
        CompleteTradeCommandHandler completeTrade,
        CancellationToken cancellationToken
    )
    {
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

    private static InventorySummary ToSummary(
        InventorySnapshot snapshot,
        IReadOnlyCollection<Guid> questItemIds
    ) => new(snapshot.Gold, CreatureEndpoints.ToItemDetails(snapshot.Items, questItemIds));
}
