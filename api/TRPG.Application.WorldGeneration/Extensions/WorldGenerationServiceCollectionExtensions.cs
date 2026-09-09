using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.WorldGeneration.Generators;

namespace TRPG.Application.WorldGeneration.Extensions;

public static class WorldGenerationServiceCollectionExtensions
{
    public static IServiceCollection AddWorldGenerationServices(
        this IServiceCollection serviceCollection
    ) =>
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
            .AddTransient<DungeonLootGenerator>()
            .AddTransient<DungeonTrapGenerator>()
            .AddTransient<DungeonPremiseGenerator>()
            .AddTransient<WildernessPopulator>()
            .AddTransient<HouseholdGenerator>()
            .AddTransient<CreatureGroupGenerator>()
            .AddTransient<CityGenerator>()
            .AddTransient<GeographyGenerator>()
            .AddTransient<BuildingGenerator>()
            .AddTransient<FactionsGenerator>()
            .AddTransient<QuestGenerator>()
            .AddTransient<WorldGenerator>();
}
