using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TRPG.Application.Caravans.Commands;
using TRPG.Application.Common.Clocks;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Concurrency;

namespace TRPG.Caravans.Endpoints;

internal static class CaravanEndpoints
{
    public static void MapCaravanEndpoints(this WebApplication app)
    {
        app.MapPut(
                "/players/{playerId:guid}/caravans/{caravanId:guid}/interaction",
                BeginInteraction
            )
            .WithName("BeginCaravanInteraction");
        app.MapDelete(
                "/players/{playerId:guid}/caravans/{caravanId:guid}/interaction",
                EndInteraction
            )
            .WithName("EndCaravanInteraction");
    }

    private static async Task<NoContent> BeginInteraction(
        Guid playerId,
        Guid caravanId,
        Guid worldId,
        [FromServices] IWorldClock worldClock,
        [FromServices] IWorldMutationGate mutationGate,
        [FromServices] ICommandHandler<BeginCaravanInteractionCommand> beginInteraction,
        CancellationToken cancellationToken
    )
    {
        await using var lease = await mutationGate.Acquire(worldId, cancellationToken);
        var gameTime = await worldClock.GetCurrent(worldId, cancellationToken);
        await beginInteraction.Handle(
            new BeginCaravanInteractionCommand
            {
                WorldId = worldId,
                PlayerId = playerId,
                CaravanId = caravanId,
                GameTime = gameTime,
            },
            cancellationToken
        );
        return TypedResults.NoContent();
    }

    private static async Task<NoContent> EndInteraction(
        Guid playerId,
        Guid caravanId,
        Guid worldId,
        [FromServices] IWorldClock worldClock,
        [FromServices] IWorldMutationGate mutationGate,
        [FromServices] ICommandHandler<EndCaravanInteractionCommand> endInteraction,
        CancellationToken cancellationToken
    )
    {
        await using var lease = await mutationGate.Acquire(worldId, cancellationToken);
        var gameTime = await worldClock.GetCurrent(worldId, cancellationToken);
        await endInteraction.Handle(
            new EndCaravanInteractionCommand
            {
                WorldId = worldId,
                PlayerId = playerId,
                CaravanId = caravanId,
                GameTime = gameTime,
            },
            cancellationToken
        );
        return TypedResults.NoContent();
    }
}
