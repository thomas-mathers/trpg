using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Abilities.Queries;
using TRPG.Application.Buildings.Commands;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.Chat;
using TRPG.Application.Chat.Commands;
using TRPG.Application.Chat.Queries;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Combat.Tools;
using TRPG.Application.Common.Tools;
using TRPG.Application.CreatureJobs.Commands;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.GameTurns;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Inventory.Tools;
using TRPG.Application.Narration.Queries;
using TRPG.Application.NpcConversations.Commands;
using TRPG.Application.NpcConversations.Queries;
using TRPG.Application.NpcConversations.Tools;
using TRPG.Application.Quests;
using TRPG.Application.Quests.Commands;
using TRPG.Application.Quests.Queries;
using TRPG.Application.Quests.Tools;
using TRPG.Application.Reputations.Commands;
using TRPG.Application.Reputations.Queries;
using TRPG.Application.Scenes.Commands;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Tools;
using TRPG.Application.Trading;
using TRPG.Application.WeaponProficiency.Commands;
using TRPG.Application.WeaponProficiency.Queries;
using TRPG.Application.Worlds.Commands;
using TRPG.Application.Worlds.Encounters.Commands;
using TRPG.Application.Worlds.Encounters.Queries;
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
            .AddTransient<AddBuildingOwnerCommandHandler>()
            .AddTransient<RemoveBuildingOwnerCommandHandler>()
            .AddTransient<SetWorkstationOccupantCommandHandler>()
            .AddTransient<SetFrontDoorLockedCommandHandler>()
            .AddTransient<GetBuildingByIdQueryHandler>()
            .AddTransient<GetRoomSummaryQueryHandler>()
            .AddTransient<GetBuildingByNameAtLocationQueryHandler>()
            .AddTransient<GetExitByDestinationNameQueryHandler>()
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
            .AddTransient<GetCreatureIdsHoldingItemsQueryHandler>()
            .AddTransient<GetInventorySummaryByOwnerQueryHandler>()
            .AddTransient<PreviewEquipItemStatsQueryHandler>()
            .AddTransient<PreviewEquipItemBasicAttackDamageQueryHandler>()
            .AddTransient<InventoryItemTransfer>()
            .AddTransient<AddGoldCommandHandler>()
            .AddTransient<ReceivePlayerInventoryCommandHandler>()
            .AddTransient<TransferPlayerInventoryCommandHandler>()
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
            .AddTransient<SyncLocationCommandHandler>()
            .AddTransient<RefreshSceneCommandHandler>()
            .AddTransient<CanEnterBuildingQueryHandler>()
            .AddTransient<GetStateByIdQueryHandler>()
            .AddTransient<GetCityByIdQueryHandler>()
            .AddTransient<GetCityByStateIdQueryHandler>()
            .AddTransient<GetDistrictByIdQueryHandler>()
            .AddTransient<GetLocationByIdQueryHandler>()
            .AddTransient<GetNpcConversationSummaryQueryHandler>()
            .AddTransient<SetNpcConversationSummaryCommandHandler>()
            .AddTransient<OpenNpcConversationCommandHandler>()
            .AddTransient<CloseNpcConversationCommandHandler>()
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
            .AddTransient<CompleteQuestCommandHandler>()
            .AddTransient<SetQuestTrackingCommandHandler>()
            .AddTransient<MarkQuestsReadyToCompleteCommandHandler>()
            .AddTransient<GetQuestJournalQueryHandler>()
            .AddTransient<GetActiveQuestItemIdsQueryHandler>()
            .AddTransient<GetQuestInteractionsForGiverQueryHandler>()
            .AddTransient<GetQuestMarkersForGiversQueryHandler>()
            .AddTransient<QuestObjectiveAdvancer>()
            .AddTransient<CreatureKilledQuestEventHandler>()
            .AddTransient<PlayerMovedQuestEventHandler>()
            .AddTransient<ConversationStartedQuestEventHandler>()
            .AddTransient<ItemAcquiredQuestEventHandler>()
            .AddTransient<GetAllReputationsByCreatureIdQueryHandler>()
            .AddTransient<GetEffectiveReputationQueryHandler>()
            .AddTransient<GetEffectiveReputationsQueryHandler>()
            .AddTransient<GetSceneQueryHandler>()
            .AddTransient<GetCurrentSceneQueryHandler>()
            .AddTransient<GetLoreAnchorsByWorldQueryHandler>()
            .AddTransient<GetLoreAnchorAutomatonByWorldQueryHandler>()
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
            .AddTransient<WildernessPopulator>()
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
            .AddTransient<LlmConversationClient>()
            .AddTransient<CloseLingeringNpcConversationsCommandHandler>()
            .AddTransient<GameTurnStreamer>()
            .AddTransient<StreamOpeningTurnHandler>()
            .AddTransient<StreamWaitTurnHandler>()
            .AddTransient<StreamChatTurnHandler>()
            .AddTransient<StreamFleeTurnHandler>()
            .AddTransient<StreamEncounterActionTurnHandler>()
            .AddTransient<ResolveCombatActionHandler>()
            .AddTransient<CreateGameSessionCommandHandler>()
            .AddTransient<GetGameSessionQueryHandler>()
            .AddTransient<GetOpenNpcConversationsQueryHandler>()
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
            .AddTransient<GetActiveEncounterQueryHandler>()
            .AddTransient<GetEncounterGroupCreatureIdsQueryHandler>()
            .AddTransient<GetEncounterGroupContextQueryHandler>()
            .AddTransient<EvaluateLocationEncountersCommandHandler>()
            .AddTransient<CompleteEncounterCommandHandler>()
            .AddTransient<ResolveEncounterActionCommandHandler>()
            .AddTransient<PublishSessionStateCommandHandler>()
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
            .AddGameTool<ShowQuestDetailsTool>()
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
