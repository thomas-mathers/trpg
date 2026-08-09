using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Trading;
using TRPG.Contracts.Inventory.Requests;
using TRPG.Contracts.Inventory.Responses;
using TRPG.Contracts.Trading.Requests;
using TRPG.Contracts.Trading.Responses;
using TRPG.Creatures.Endpoints;
using TRPG.Data.Models;

namespace TRPG.Inventory.Endpoints;

internal static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this WebApplication app)
    {
        app.MapPost("/inventory-transfers", InventoryTransfer)
            .WithName("TransferInventory")
            .ProducesProblem(StatusCodes.Status400BadRequest);
        app.MapGet("/players/{playerId:guid}/trades/{workstationId:guid}", GetTrade)
            .WithName("GetTrade");
        app.MapPost("/players/{playerId:guid}/trades/{workstationId:guid}/proposal", ProposeTrade)
            .WithName("ProposeTrade");
        app.MapPost("/players/{playerId:guid}/trades/{workstationId:guid}/complete", CompleteTrade)
            .WithName("CompleteTrade");
    }

    private static async Task<Results<NotFound, ProblemHttpResult, NoContent>> InventoryTransfer(
        InventoryTransferRequest request,
        GetCreatureByIdQueryHandler getCreatureById,
        InventoryTransferCommandHandler transfer,
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

        await transfer.Handle(
            new InventoryTransferCommand
            {
                From = new ItemOwnerReference(request.FromId, OwnerType.Creature),
                To = new ItemOwnerReference(request.ToId, OwnerType.Creature),
                Items = request.Items,
            },
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    private static async Task<Ok<TradeSnapshot>> GetTrade(
        Guid playerId,
        Guid workstationId,
        GetTradeQueryHandler getTrade,
        CancellationToken cancellationToken
    )
    {
        var trade = await getTrade.Handle(
            new GetTradeQuery { PlayerId = playerId, WorkstationId = workstationId },
            cancellationToken
        );
        return TypedResults.Ok(
            new TradeSnapshot(ToSummary(trade.PlayerInventory), ToSummary(trade.ShopInventory))
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
        TradeRequest request,
        CompleteTradeCommandHandler completeTrade,
        CancellationToken cancellationToken
    )
    {
        await completeTrade.Handle(
            new CompleteTradeCommand
            {
                PlayerId = playerId,
                WorkstationId = workstationId,
                PlayerOffer = request.PlayerOffer,
                ShopOffer = request.ShopOffer,
            },
            cancellationToken
        );
        return TypedResults.NoContent();
    }

    private static InventorySummary ToSummary(InventorySnapshot snapshot) =>
        new(snapshot.Gold, snapshot.Items.Select(CreatureEndpoints.ToItemDetail).ToArray());
}
