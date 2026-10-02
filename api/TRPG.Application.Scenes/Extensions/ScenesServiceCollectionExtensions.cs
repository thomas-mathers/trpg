using Microsoft.Extensions.DependencyInjection;

namespace TRPG.Application.Scenes.Extensions;

public static class ScenesServiceCollectionExtensions
{
    public static IServiceCollection AddScenesServices(this IServiceCollection serviceCollection) =>
        serviceCollection.AddSingleton<PublishedSceneRegistry>().AddTransient<ScenePublisher>();
}
