using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Abilities;
using TRPG.Application.Buildings.Commands;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.Combat;
using TRPG.Application.Conversations.Commands;
using TRPG.Application.Conversations.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Game;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Jobs.Commands;
using TRPG.Application.Jobs.Queries;
using TRPG.Application.Reputations.Commands;
using TRPG.Application.Reputations.Queries;
using TRPG.Application.Scenes.Commands;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Tools;
using TRPG.Application.WeaponProficiency.Commands;
using TRPG.Application.WeaponProficiency.Queries;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Generators;
using TRPG.Application.Worlds.Queries;

namespace TRPG.Application.Common.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTrpgApplicationServices(
        this IServiceCollection serviceCollection
    )
    {
        return serviceCollection
            .AddMemoryCache()
            .AddTransient<AddBuildingOwnerCommandHandler>()
            .AddTransient<RemoveBuildingOwnerCommandHandler>()
            .AddTransient<SetWorkstationOccupantCommandHandler>()
            .AddTransient<SetFrontDoorLockedCommandHandler>()
            .AddTransient<GetBuildingByIdQueryHandler>()
            .AddTransient<GetRoomSummaryQueryHandler>()
            .AddTransient<GetBuildingByNameInStateQueryHandler>()
            .AddTransient<GetEntranceRoomQueryHandler>()
            .AddTransient<GetExitByDestinationNameQueryHandler>()
            .AddTransient<GetRoomsByIdsQueryHandler>()
            .AddTransient<GetAllBuildingsByStateIdQueryHandler>()
            .AddTransient<GetStaticPropsByRoomIdQueryHandler>()
            .AddTransient<GetConnectorsByRoomIdQueryHandler>()
            .AddTransient<GetWorkstationsByRoomIdQueryHandler>()
            .AddTransient<GetAllOwnersByBuildingIdQueryHandler>()
            .AddTransient<GetAllBuildingsByLocationQueryHandler>()
            .AddTransient<GetFrontDoorQueryHandler>()
            .AddTransient<GetKeyItemIdsQueryHandler>()
            .AddTransient<AddInventoryItemCommandHandler>()
            .AddTransient<EquipInventoryItemCommandHandler>()
            .AddTransient<UnequipInventoryItemCommandHandler>()
            .AddTransient<RemoveInventoryItemCommandHandler>()
            .AddTransient<GetInventoryByCreatureIdQueryHandler>()
            .AddTransient<AddJobCommandHandler>()
            .AddTransient<UpdateJobCommandHandler>()
            .AddTransient<DeleteJobCommandHandler>()
            .AddTransient<GetAllJobsByCreatureIdQueryHandler>()
            .AddTransient<GetJobsOfBuildingWorkersQueryHandler>()
            .AddTransient<GetCreatureIdsWithJobInRoomQueryHandler>()
            .AddTransient<ExecuteJobCommandHandler>()
            .AddTransient<SyncScheduleLockCommandHandler>()
            .AddTransient<SyncCommandHandler>()
            .AddTransient<CanEnterBuildingQueryHandler>()
            .AddTransient<GetCountryByIdQueryHandler>()
            .AddTransient<GetAllCountriesByWorldIdQueryHandler>()
            .AddTransient<GetStateByIdQueryHandler>()
            .AddTransient<GetAllStatesByCountryIdQueryHandler>()
            .AddTransient<GetCityByIdQueryHandler>()
            .AddTransient<GetCityByStateIdQueryHandler>()
            .AddTransient<GetAllDistrictsByCityIdQueryHandler>()
            .AddTransient<GetDistrictByNameInCityQueryHandler>()
            .AddTransient<GetConversationSummaryQueryHandler>()
            .AddTransient<SetConversationSummaryCommandHandler>()
            .AddSingleton(AbilityDefinitions.Create())
            .AddSingleton(new CreatureGeneratorSettings())
            .AddTransient<AddCreatureCommandHandler>()
            .AddTransient<UpdateCreatureCommandHandler>()
            .AddTransient<ApplyCombatRewardsCommandHandler>()
            .AddTransient<DeleteCreatureCommandHandler>()
            .AddTransient<GetCreatureByIdQueryHandler>()
            .AddTransient<GetAllCreaturesInStateQueryHandler>()
            .AddTransient<GetCreatureIdsByDistrictQueryHandler>()
            .AddTransient<GetCreatureByNameInRoomQueryHandler>()
            .AddTransient<GetCreatureByNameOutdoorsInStateQueryHandler>()
            .AddTransient<GetCreatureByNameOutdoorsInDistrictQueryHandler>()
            .AddTransient<GetCreatureByNameNearbyQueryHandler>()
            .AddTransient<GetAllNearbyCreaturesQueryHandler>()
            .AddTransient<GetCreatureAbilitiesQueryHandler>()
            .AddTransient<GetCreatureKnowledgeQueryHandler>()
            .AddTransient<AdjustReputationCommandHandler>()
            .AddTransient<GetAllReputationsByCreatureIdQueryHandler>()
            .AddTransient<GetEffectiveReputationQueryHandler>()
            .AddTransient<GetEffectiveReputationsQueryHandler>()
            .AddTransient<GetSceneQueryHandler>()
            .AddTransient<GetSceneWithCatchUpQueryHandler>()
            .AddTransient<GetWorldQueryHandler>()
            .AddTransient<GetAllWorldsQueryHandler>()
            .AddTransient<UpdateWorldCommandHandler>()
            .AddTransient<WeaponGenerator>()
            .AddTransient<ArmorGenerator>()
            .AddTransient<AccessoryGenerator>()
            .AddTransient<ConsumableGenerator>()
            .AddTransient<AmmoGenerator>()
            .AddTransient<ItemGenerator>()
            .AddTransient<CreatureGenerator>()
            .AddTransient<HouseholdGenerator>()
            .AddTransient<CityGenerator>()
            .AddTransient<GeographyGenerator>()
            .AddTransient<BuildingGenerator>()
            .AddTransient<FactionsGenerator>()
            .AddTransient<WorldGenerator>()
            .AddTransient<CreateWorldCommandHandler>()
            .AddTransient<BootstrapWorldCommandHandler>()
            .AddTransient<DropWorldCommandHandler>()
            .AddTransient<GameTurnRunner>()
            .AddTransient<GetAllWeaponProficienciesQueryHandler>()
            .AddTransient<SetWeaponProficiencyCommandHandler>()
            .AddTransient<ApplyWeaponSwingGainsCommandHandler>()
            .AddSingleton(new CombatSettings())
            .AddTransient<HitCalculator>()
            .AddTransient<DamageCalculator>()
            .AddTransient<CombatEngine>()
            .AddGameTool<WorldInfoTool>()
            .AddGameTool<LookTool>()
            .AddGameTool<MoveTool>()
            .AddGameTool<InventoryTool>()
            .AddGameTool<CharacterTool>()
            .AddGameTool<StartConversationTool>()
            .AddGameTool<EndConversationTool>()
            .AddGameTool<LookupTool>()
            .AddGameTool<AttackTool>()
            .AddGameTool<FleeTool>();
    }

    private static IServiceCollection AddGameTool<T>(this IServiceCollection serviceCollection)
        where T : class, IGameTool =>
        serviceCollection
            .AddScoped<T>()
            .AddScoped<AIFunction>(sp =>
                AIFunctionFactory.Create(
                    sp.GetRequiredService<T>().Invoke,
                    new AIFunctionFactoryOptions { SerializerOptions = ToolJsonOptions.Options }
                )
            );
}
