using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Abilities;
using TRPG.Application.Buildings.Commands;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Combat.Tools;
using TRPG.Application.Conversations.Commands;
using TRPG.Application.Conversations.Queries;
using TRPG.Application.Conversations.Tools;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Game;
using TRPG.Application.Game.Commands;
using TRPG.Application.Game.Queries;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Inventory.Tools;
using TRPG.Application.Jobs.Commands;
using TRPG.Application.Jobs.Queries;
using TRPG.Application.Reputations.Commands;
using TRPG.Application.Reputations.Queries;
using TRPG.Application.Scenes.Commands;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Tools;
using TRPG.Application.Tools.Common;
using TRPG.Application.WeaponProficiency.Commands;
using TRPG.Application.WeaponProficiency.Queries;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Generators;
using TRPG.Application.Worlds.Queries;
using TRPG.Application.Worlds.Tools;

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
            .AddTransient<AddCreatureCommandHandler>()
            .AddTransient<UpdateCreaturesCommandHandler>()
            .AddTransient<ApplyCombatRewardsCommandHandler>()
            .AddTransient<GrantAllAbilitiesCommandHandler>()
            .AddTransient<DeleteCreaturesCommandHandler>()
            .AddTransient<GetCreatureByIdQueryHandler>()
            .AddTransient<GetCreaturesByIdsQueryHandler>()
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
            .AddTransient<SetWorldPlaytimeCommandHandler>()
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
            .AddTransient<GameSessionLocks>()
            .AddTransient<CreateGameSessionCommandHandler>()
            .AddTransient<GetGameSessionQueryHandler>()
            .AddTransient<GetOpenConversationsQueryHandler>()
            .AddTransient<GetPlaytimeQueryHandler>()
            .AddTransient<AdvanceTimeCommandHandler>()
            .AddTransient<UpdateGameSessionCommandHandler>()
            .AddTransient<DeleteGameSessionCommandHandler>()
            .AddTransient<EndGameSessionCommandHandler>()
            .AddTransient<GetChatMessagesQueryHandler>()
            .AddTransient<AppendChatMessagesCommandHandler>()
            .AddTransient<ClearChatMessagesCommandHandler>()
            .AddTransient<GetCombatantsQueryHandler>()
            .AddTransient<SetCombatantsCommandHandler>()
            .AddTransient<ClearCombatantsCommandHandler>()
            .AddTransient<GetAllWeaponProficienciesQueryHandler>()
            .AddTransient<AdjustWeaponProficienciesCommandHandler>()
            .AddTransient<HitCalculator>()
            .AddTransient<DamageCalculator>()
            .AddTransient<CombatEngine>()
            .AddGameTool<WorldInfoTool>()
            .AddGameTool<LookTool>()
            .AddGameTool<MoveTool>()
            .AddGameTool<InventoryTool>()
            .AddGameTool<CreatureInspectTool>()
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
