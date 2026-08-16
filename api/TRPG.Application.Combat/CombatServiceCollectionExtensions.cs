using Microsoft.Extensions.DependencyInjection;

namespace TRPG.Application.Combat;

public static class CombatServiceCollectionExtensions
{
    public static IServiceCollection AddCombatServices(this IServiceCollection serviceCollection) =>
        serviceCollection
            .AddTransient<HitCalculator>()
            .AddTransient<DamageCalculator>()
            .AddTransient<EnemyCombatActionResolver>()
            .AddTransient<CombatEngine>();
}
