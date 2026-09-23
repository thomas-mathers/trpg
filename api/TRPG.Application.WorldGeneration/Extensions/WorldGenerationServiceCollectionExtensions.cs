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
            .AddTransient<DungeonExpeditionGenerator>()
            .AddTransient<DungeonInhabitantGenerator>()
            .AddTransient<DungeonLootGenerator>()
            .AddTransient<DungeonTrapGenerator>()
            .AddTransient<DungeonObstacleGenerator>()
            .AddTransient<DungeonPremiseGenerator>()
            .AddTransient<WildernessPopulator>()
            .AddTransient<HouseholdGenerator>()
            .AddTransient<CreatureGroupGenerator>()
            .AddTransient<CityGenerator>()
            .AddTransient<CountryPatrolRouteSeeder>()
            .AddTransient<RoadTravelerRouteSeeder>()
            .AddTransient<GeographyGenerator>()
            .AddTransient<BuildingGenerator>()
            .AddTransient<QuestChainContentGenerator>()
            .AddTransient<QuestChainStoryBibleGenerator>()
            .AddTransient<QuestChainBlockGraphComposer>()
            .AddTransient<QuestChainCastingGenerator>()
            .AddTransient<QuestChainFactDisclosureRepairer>()
            .AddTransient<QuestChainGlobalGraphPipeline>()
            .AddTransient<QuestChainTreatmentFirstGenerator>()
            .AddSingleton(Random.Shared)
            .AddTransient<WorldGenerator>();
}
