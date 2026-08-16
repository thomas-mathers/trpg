using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Generators;

namespace TRPG.Application.Worlds.Extensions;

public static class WorldsServiceCollectionExtensions
{
    public static IServiceCollection AddWorldsServices(this IServiceCollection serviceCollection) =>
        serviceCollection
            .AddTransient<WeaponGenerator>()
            .AddTransient<ArmorGenerator>()
            .AddTransient<AccessoryGenerator>()
            .AddTransient<ConsumableGenerator>()
            .AddTransient<AmmoGenerator>()
            .AddTransient<ItemGenerator>()
            .AddTransient<TradeStockGenerator>()
            .AddTransient<CreatureGenerator>()
            .AddTransient<DungeonPopulator>()
            .AddTransient<WildernessPopulator>()
            .AddTransient<HouseholdGenerator>()
            .AddTransient<CityGenerator>()
            .AddTransient<GeographyGenerator>()
            .AddTransient<BuildingGenerator>()
            .AddTransient<FactionsGenerator>()
            .AddTransient<QuestGenerator>()
            .AddTransient<WorldGenerator>()
            .AddTransient<BootstrapWorldCommandHandler>();
}
