using Microsoft.Extensions.DependencyInjection;
using OllamaSharp.Models.Chat;
using TRPG.Application.Abilities;
using TRPG.Application.Buildings.Commands;
using TRPG.Application.Buildings.Queries;
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
            .AddTransient<GetAllDistrictsByCityIdQueryHandler>()
            .AddTransient<GetDistrictByNameInCityQueryHandler>()
            .AddTransient<GetConversationSummaryQueryHandler>()
            .AddTransient<SetConversationSummaryCommandHandler>()
            .AddSingleton(AbilityDefinitions.Create())
            .AddSingleton(new CreatureGeneratorSettings())
            .AddTransient<AddCreatureCommandHandler>()
            .AddTransient<UpdateCreatureCommandHandler>()
            .AddTransient<DeleteCreatureCommandHandler>()
            .AddTransient<GetCreatureByIdQueryHandler>()
            .AddTransient<GetAllCreaturesInStateQueryHandler>()
            .AddTransient<GetCreatureIdsByDistrictQueryHandler>()
            .AddTransient<GetCreatureByNameInRoomQueryHandler>()
            .AddTransient<GetCreatureByNameOutdoorsInStateQueryHandler>()
            .AddTransient<GetCreatureByNameOutdoorsInDistrictQueryHandler>()
            .AddTransient<GetCreatureByNameNearbyQueryHandler>()
            .AddTransient<GetAllNearbyCreaturesQueryHandler>()
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
            .AddTransient<GeographyGenerator>()
            .AddTransient<BuildingGenerator>()
            .AddTransient<FactionsGenerator>()
            .AddTransient<WorldGenerator>()
            .AddTransient<CreateWorldCommandHandler>()
            .AddTransient<BootstrapWorldCommandHandler>()
            .AddTransient<DropWorldCommandHandler>()
            .AddTransient<GameTurnRunner>()
            .AddScoped<Tool, WorldInfoTool>()
            .AddScoped<Tool, LookTool>()
            .AddScoped<Tool, MoveTool>()
            .AddScoped<Tool, InventoryTool>()
            .AddScoped<Tool, CharacterTool>()
            .AddScoped<Tool, StartConversationTool>()
            .AddScoped<Tool, EndConversationTool>()
            .AddScoped<Tool, LookupTool>();
    }
}
