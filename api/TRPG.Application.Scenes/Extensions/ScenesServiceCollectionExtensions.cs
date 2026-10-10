using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Scenes.Queries;

namespace TRPG.Application.Scenes.Extensions;

public static class ScenesServiceCollectionExtensions
{
    public static IServiceCollection AddScenesServices(this IServiceCollection serviceCollection) =>
        serviceCollection
            .AddSingleton<PublishedSceneRegistry>()
            .AddSingleton<TransientCreatureWalkRegistry>()
            .AddTransient<ScenePublisher>()
            .AddTransient<SceneCreatureInfoBuilder>();
}
