using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Scenes.Results;
using TRPG.Application.Worlds.Commands;
using TRPG.Domain;

namespace TRPG.Tests.Helpers;

internal static class PublishedSceneBaseline
{
    public static async Task PublishBaselineScene(
        this IServiceProvider services,
        Guid worldId,
        Guid playerId,
        GameInstant gameTime,
        CancellationToken cancellationToken
    )
    {
        var scene = await services
            .GetRequiredService<IQueryHandler<GetCurrentSceneQuery, SceneResult>>()
            .Handle(
                new GetCurrentSceneQuery
                {
                    WorldId = worldId,
                    PlayerId = playerId,
                    GameTime = gameTime,
                },
                cancellationToken
            );
        var stamp = await services
            .GetRequiredService<ICommandHandler<StampWorldStateCommand, WorldStateStamp>>()
            .Handle(new StampWorldStateCommand { WorldId = worldId }, cancellationToken);

        services.GetRequiredService<ScenePublisher>().Publish(worldId, playerId, scene, stamp);
    }
}
