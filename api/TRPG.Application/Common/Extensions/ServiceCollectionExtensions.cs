using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Abilities.Queries;
using TRPG.Application.Buildings.Commands;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Combat.Tools;
using TRPG.Application.Common.Events;
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
using TRPG.Application.Quests;
using TRPG.Application.Quests.Commands;
using TRPG.Application.Reputations.Commands;
using TRPG.Application.Reputations.Queries;
using TRPG.Application.Scenes.Commands;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Tools;
using TRPG.Application.Trading;
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
            .AddScoped<GameTurnContext>()
            .AddTransient<DomainEventTransactionRunner>()
            .AddTransient<AddBuildingOwnerCommandHandler>()
            .AddTransient<RemoveBuildingOwnerCommandHandler>()
            .AddTransient<SetWorkstationOccupantCommandHandler>()
            .AddTransient<SetFrontDoorLockedCommandHandler>()
            .AddTransient<GetBuildingByIdQueryHandler>()
            .AddTransient<GetRoomSummaryQueryHandler>()
            .AddTransient<GetBuildingByNameAtLocationQueryHandler>()
            .AddTransient<GetExitByDestinationNameQueryHandler>()
            .AddTransient<GetAllBuildingsByStateIdQueryHandler>()
            .AddTransient<GetStaticPropsByLocationIdQueryHandler>()
            .AddTransient<GetConnectorsByLocationIdQueryHandler>()
            .AddTransient<GetWorkstationsByLocationIdQueryHandler>()
            .AddTransient<GetAllOwnersByBuildingIdQueryHandler>()
            .AddTransient<GetAllBuildingsByLocationQueryHandler>()
            .AddTransient<GetKeyItemIdsQueryHandler>()
            .AddTransient<EquipInventoryItemCommandHandler>()
            .AddTransient<UnequipInventoryItemCommandHandler>()
            .AddTransient<RemoveInventoryItemCommandHandler>()
            .AddTransient<GetInventoryByOwnerQueryHandler>()
            .AddTransient<GetInventorySummaryByOwnerQueryHandler>()
            .AddTransient<PreviewEquipItemStatsQueryHandler>()
            .AddTransient<PreviewEquipItemBasicAttackDamageQueryHandler>()
            .AddTransient<InventoryTransferCommandHandler>()
            .AddTransient<TradeOfferValidator>()
            .AddTransient<TradeOfferEvaluator>()
            .AddTransient<ProposeTradeCommandHandler>()
            .AddTransient<CompleteTradeCommandHandler>()
            .AddTransient<GetTradeQueryHandler>()
            .AddTransient<AddCreatureJobCommandHandler>()
            .AddTransient<DeleteCreatureJobCommandHandler>()
            .AddTransient<GetAllCreatureJobsByCreatureIdQueryHandler>()
            .AddTransient<GetCreatureJobsOfBuildingWorkersQueryHandler>()
            .AddTransient<GetCreatureIdsWithCreatureJobInLocationQueryHandler>()
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
            .AddTransient<GetDistrictByIdQueryHandler>()
            .AddTransient<GetLocationByIdQueryHandler>()
            .AddTransient<GetConversationSummaryQueryHandler>()
            .AddTransient<SetConversationSummaryCommandHandler>()
            .AddTransient<OpenConversationCommandHandler>()
            .AddTransient<CloseConversationCommandHandler>()
            .AddTransient<AddCreatureCommandHandler>()
            .AddTransient<UpdateCreaturesCommandHandler>()
            .AddTransient<MovePlayerCommandHandler>()
            .AddTransient<StatFormulas>()
            .AddTransient<AdjustCreatureSkillsCommandHandler>()
            .AddTransient<GetUnallocatedAttributePointsQueryHandler>()
            .AddTransient<AllocateAttributePointsCommandHandler>()
            .AddTransient<GetCreatureBaseAttributesQueryHandler>()
            .AddTransient<GetCreatureEffectiveStatsQueryHandler>()
            .AddTransient<GetCreatureBasicAttackDamageQueryHandler>()
            .AddTransient<GetCreatureSkillsQueryHandler>()
            .AddTransient<GetCreatureLevelQueryHandler>()
            .AddTransient<ApplyPassiveRegenCommandHandler>()
            .AddTransient<DeleteCreaturesCommandHandler>()
            .AddTransient<GetCreatureByIdQueryHandler>()
            .AddTransient<GetCreaturesByIdsQueryHandler>()
            .AddTransient<GetAllCreaturesInStateQueryHandler>()
            .AddTransient<GetCreatureIdsByDistrictQueryHandler>()
            .AddTransient<GetCreatureByNameAtLocationQueryHandler>()
            .AddTransient<GetCreaturesAtLocationQueryHandler>()
            .AddTransient<GetNearbyCreaturesQueryHandler>()
            .AddTransient<GetNearbyCorpsesQueryHandler>()
            .AddTransient<GetTotalCharacterXpFromSkillsQueryHandler>()
            .AddTransient<GetCreatureAbilitiesQueryHandler>()
            .AddTransient<GetAbilitiesBySkillQueryHandler>()
            .AddTransient<GetCreatureKnowledgeQueryHandler>()
            .AddTransient<AdjustReputationCommandHandler>()
            .AddTransient<AcceptQuestCommandHandler>()
            .AddTransient<GameDomainEventListener, QuestObjectiveDomainEventListener>()
            .AddTransient<GetAllReputationsByCreatureIdQueryHandler>()
            .AddTransient<GetEffectiveReputationQueryHandler>()
            .AddTransient<GetEffectiveReputationsQueryHandler>()
            .AddTransient<GetSceneQueryHandler>()
            .AddTransient<GetSceneWithCatchUpQueryHandler>()
            .AddTransient<GetNamedEntitiesByWorldQueryHandler>()
            .AddTransient<GetEntityNameAutomatonByWorldQueryHandler>()
            .AddTransient<GetWorldQueryHandler>()
            .AddTransient<GetAllWorldsQueryHandler>()
            .AddTransient<SetWorldPlaytimeCommandHandler>()
            .AddTransient<WeaponGenerator>()
            .AddTransient<ArmorGenerator>()
            .AddTransient<AccessoryGenerator>()
            .AddTransient<ConsumableGenerator>()
            .AddTransient<AmmoGenerator>()
            .AddTransient<ItemGenerator>()
            .AddTransient<TradeStockGenerator>()
            .AddTransient<CreatureGenerator>()
            .AddTransient<DungeonPopulator>()
            .AddTransient<HouseholdGenerator>()
            .AddTransient<CityGenerator>()
            .AddTransient<GeographyGenerator>()
            .AddTransient<BuildingGenerator>()
            .AddTransient<FactionsGenerator>()
            .AddTransient<QuestGenerator>()
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
            .AddTransient<GetActiveFightCombatantsQueryHandler>()
            .AddTransient<GetAbilityAvailabilityQueryHandler>()
            .AddTransient<GetCombatantQueryHandler>()
            .AddTransient<PersistCombatantsCommandHandler>()
            .AddTransient<StartFightCommandHandler>()
            .AddTransient<EndFightCommandHandler>()
            .AddTransient<AbandonActiveFightCommandHandler>()
            .AddTransient<ResolveCombatRoundCommandHandler>()
            .AddTransient<GetAllWeaponProficienciesQueryHandler>()
            .AddTransient<AdjustWeaponProficienciesCommandHandler>()
            .AddTransient<HitCalculator>()
            .AddTransient<DamageCalculator>()
            .AddTransient<EnemyCombatActionResolver>()
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
