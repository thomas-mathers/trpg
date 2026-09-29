using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Effects;

namespace TRPG.Application.Effects.Extensions;

public static class EffectsServiceCollectionExtensions
{
    public static IServiceCollection AddEffectsServices(this IServiceCollection services) =>
        services.AddSingleton<EffectTimeScale>().AddTransient<EffectAdvancer>();
}
