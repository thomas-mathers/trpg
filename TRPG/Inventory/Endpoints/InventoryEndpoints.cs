using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using TRPG.Application.Inventory.Commands;
using TRPG.Contracts.Inventory.Requests;

namespace TRPG.Inventory.Endpoints;

internal static class InventoryEndpoints
{
    public static void MapInventoryEndpoints(this WebApplication app)
    {
        app.MapPost("/transfers", InventoryTransfer);
    }

    private static async Task<IResult> InventoryTransfer(
        Guid fromId,
        Guid toId,
        InventoryTransferRequest request,
        InventoryTransferCommandHandler transfer,
        CancellationToken cancellationToken
    )
    {
        await transfer.Handle(
            new InventoryTransferCommand
            {
                FromCreatureId = fromId,
                ToCreatureId = toId,
                Items = request.Items,
            },
            cancellationToken
        );

        return Results.NoContent();
    }
}
