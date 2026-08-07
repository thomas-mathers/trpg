using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory.Commands;
using TRPG.Contracts.Inventory.Requests;
using TRPG.Data.Models;

namespace TRPG.Inventory.Endpoints;

internal static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this WebApplication app)
    {
        app.MapPost("/inventory-transfers", InventoryTransfer)
            .WithName("TransferInventory")
            .ProducesProblem(StatusCodes.Status400BadRequest);
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
}
