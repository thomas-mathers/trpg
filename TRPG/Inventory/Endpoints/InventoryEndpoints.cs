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
        app.MapPost("/transfers", InventoryTransfer);
    }

    private static async Task<Results<NotFound, BadRequest, NoContent>> InventoryTransfer(
        Guid fromId,
        Guid toId,
        InventoryTransferRequest request,
        GetCreatureByIdQueryHandler getCreatureById,
        InventoryTransferCommandHandler transfer,
        CancellationToken cancellationToken
    )
    {
        var fromCreature = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = fromId },
            cancellationToken
        );
        if (fromCreature == null)
        {
            return TypedResults.NotFound();
        }

        var toCreature = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = toId },
            cancellationToken
        );
        if (toCreature == null)
        {
            return TypedResults.NotFound();
        }

        if (fromCreature.LocationId != toCreature.LocationId)
        {
            return TypedResults.BadRequest();
        }

        await transfer.Handle(
            new InventoryTransferCommand
            {
                From = new ItemOwnerReference(fromId, OwnerType.Creature),
                To = new ItemOwnerReference(toId, OwnerType.Creature),
                Items = request.Items,
            },
            cancellationToken
        );

        return TypedResults.NoContent();
    }
}
