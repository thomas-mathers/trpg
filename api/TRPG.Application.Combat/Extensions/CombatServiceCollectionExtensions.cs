using Microsoft.Extensions.DependencyInjection;

namespace TRPG.Application.Combat.Extensions;

public static class CombatServiceCollectionExtensions
{
    public static IServiceCollection AddCombatServices(this IServiceCollection serviceCollection) =>
        serviceCollection
            .AddTransient<HitCalculator>()
            .AddTransient<DamageCalculator>()
            .AddTransient<EnemyCombatActionResolver>()
            .AddSingleton<CombatTimeScale>()
            .AddTransient<EffectAdvancer>()
            .AddTransient<CombatEngine>();
}
