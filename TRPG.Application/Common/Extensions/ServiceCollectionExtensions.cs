using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Abilities;
using TRPG.Application.Abilities.Queries;
using TRPG.Application.Buildings.Commands;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Combat.Tools;
using TRPG.Application.Common.Tools;
using TRPG.Application.Conversations.Commands;
using TRPG.Application.Conversations.Queries;
using TRPG.Application.Conversations.Tools;
using TRPG.Application.CreatureJobs.Commands;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Inventory.Tools;
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
            .AddTransient<AddCreatureJobCommandHandler>()
            .AddTransient<DeleteCreatureJobCommandHandler>()
            .AddTransient<GetAllCreatureJobsByCreatureIdQueryHandler>()
            .AddTransient<GetCreatureJobsOfBuildingWorkersQueryHandler>()
            .AddTransient<GetCreatureIdsWithCreatureJobInRoomQueryHandler>()
            .AddTransient<ExecuteCreatureJobCommandHandler>()
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
            .AddTransient<OpenConversationCommandHandler>()
            .AddTransient<CloseConversationCommandHandler>()
            .AddSingleton(AbilityDefinitions.Create())
            .AddTransient<AddCreatureCommandHandler>()
            .AddTransient<UpdateCreaturesCommandHandler>()
            .AddTransient<MovePlayerCommandHandler>()
            .AddTransient<StatFormulas>()
            .AddTransient<ApplyCombatRewardsCommandHandler>()
            .AddTransient<AdjustCreatureSkillsCommandHandler>()
            .AddTransient<GetUnallocatedAttributePointsQueryHandler>()
            .AddTransient<AllocateAttributePointsCommandHandler>()
            .AddTransient<GetCreatureBaseAttributesQueryHandler>()
            .AddTransient<GetCreatureSkillsQueryHandler>()
            .AddTransient<GetCreatureLevelQueryHandler>()
            .AddTransient<ApplyPassiveRegenCommandHandler>()
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
            .AddTransient<GetTotalCharacterXpFromSkillsQueryHandler>()
            .AddTransient<GetCreatureAbilitiesQueryHandler>()
            .AddTransient<GetCreatureKnowledgeQueryHandler>()
            .AddTransient<AdjustReputationCommandHandler>()
            .AddTransient<GetAllReputationsByCreatureIdQueryHandler>()
            .AddTransient<GetEffectiveReputationQueryHandler>()
            .AddTransient<GetEffectiveReputationsQueryHandler>()
            .AddTransient<GetSceneQueryHandler>()
            .AddTransient<GetSceneWithCatchUpQueryHandler>()
            .AddTransient<GetNamedEntitiesByWorldQueryHandler>()
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
            .AddTransient<DungeonPopulator>()
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
            .AddTransient<GetActiveFightQueryHandler>()
            .AddTransient<GetCombatantsQueryHandler>()
            .AddTransient<PersistCombatantsCommandHandler>()
            .AddTransient<StartFightCommandHandler>()
            .AddTransient<EndFightCommandHandler>()
            .AddTransient<ResolveCombatRoundCommandHandler>()
            .AddTransient<GetUsableAbilitiesQueryHandler>()
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
            .AddGameTool<StartFightTool>();
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
