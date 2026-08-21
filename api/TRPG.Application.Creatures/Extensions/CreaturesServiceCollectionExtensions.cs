using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Creatures;

namespace TRPG.Application.Creatures.Extensions;

public static class CreaturesServiceCollectionExtensions
{
    public static IServiceCollection AddCreaturesServices(
        this IServiceCollection serviceCollection
    ) =>
        serviceCollection
            .AddSingleton<IChanceRoller, ChanceRoller>()
            .AddTransient<SkillCheckService>();
}
