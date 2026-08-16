using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Abilities.Queries;
using TRPG.Application.Buildings.Commands;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.Chat.Commands;
using TRPG.Application.Chat.Queries;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Handling;
using TRPG.Application.CreatureJobs.Commands;
using TRPG.Application.CreatureJobs.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.GameTurns;
using TRPG.Application.GameTurns.Commands;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Narration.Queries;
using TRPG.Application.NpcConversations.Commands;
using TRPG.Application.NpcConversations.Queries;
using TRPG.Application.Quests;
using TRPG.Application.Quests.Commands;
using TRPG.Application.Quests.Events;
using TRPG.Application.Quests.Queries;
using TRPG.Application.Reputations.Commands;
using TRPG.Application.Reputations.Queries;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Commands;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Trading;
using TRPG.Application.WeaponProficiency.Commands;
using TRPG.Application.WeaponProficiency.Queries;
using TRPG.Application.Worlds;
using TRPG.Application.Worlds.Queries;
using TRPG.GameTurns.Tools;
using TRPG.Handling;
using TRPG.Tools;

namespace TRPG.Extensions;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddTrpgApplicationServices(
        this IServiceCollection serviceCollection
    )
    {
        return serviceCollection
            .AddMemoryCache()
            .AddScoped<GameTurnContext>()
            .AddTransient(typeof(ICommandValidator<>), typeof(DataAnnotationsCommandValidator<>))
            .AddTransient(typeof(IDomainEventPublisher<>), typeof(DomainEventPublisher<>))
            .Scan(scan =>
                scan.FromApplicationDependencies()
                    .AddClasses(
                        classes => classes.AssignableTo(typeof(ICommandHandler<>)),
                        publicOnly: false
                    )
                    .AsSelfWithInterfaces()
                    .WithTransientLifetime()
                    .AddClasses(
                        classes => classes.AssignableTo(typeof(ICommandHandler<,>)),
                        publicOnly: false
                    )
                    .AsSelfWithInterfaces()
                    .WithTransientLifetime()
                    .AddClasses(
                        classes => classes.AssignableTo(typeof(IQueryHandler<,>)),
                        publicOnly: false
                    )
                    .AsSelfWithInterfaces()
                    .WithTransientLifetime()
            )
            .AddTradingServices()
            .AddTransient<SceneCatchUpCache>()
            .AddTransient<QuestObjectiveAdvancer>()
            .AddTransient<CreatureKilledQuestEventHandler>()
            .AddTransient<IDomainEventConsumer<CreatureKilledEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<CreatureKilledQuestEventHandler>()
            )
            .AddTransient<PlayerMovedQuestEventHandler>()
            .AddTransient<ConversationStartedQuestEventHandler>()
            .AddTransient<IDomainEventConsumer<NpcConversationStartedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<ConversationStartedQuestEventHandler>()
            )
            .AddTransient<ItemAcquiredQuestEventHandler>()
            .AddTransient<IDomainEventConsumer<ItemAcquiredEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<ItemAcquiredQuestEventHandler>()
            )
            .AddTransient<IDomainEventConsumer<PlayerMovedEvent>>(serviceProvider =>
                serviceProvider.GetRequiredService<PlayerMovedQuestEventHandler>()
            )
            .AddWorldsServices()
            .AddTransient<LlmConversationClient>()
            .AddTransient<GameTurnStreamer>()
            .AddTransient<StreamOpeningTurnHandler>()
            .AddTransient<StreamWaitTurnHandler>()
            .AddTransient<StreamChatTurnHandler>()
            .AddTransient<StreamFleeTurnHandler>()
            .AddTransient<StreamEncounterActionTurnHandler>()
            .AddTransient<ResolveCombatActionHandler>()
            .AddTransient<HitCalculator>()
            .AddTransient<DamageCalculator>()
            .AddTransient<EnemyCombatActionResolver>()
            .AddTransient<CombatEngine>()
            .AddGameTool<LookTool>()
            .AddGameTool<MoveTool>()
            .AddGameTool<CreatureInspectTool>()
            .AddGameTool<LookupTool>()
            .Decorate(typeof(ICommandHandler<>), typeof(ValidatingCommandHandler<>))
            .Decorate(typeof(ICommandHandler<,>), typeof(ValidatingCommandHandler<,>))
            .Decorate(typeof(ICommandHandler<>), typeof(LoggedCommandHandler<>))
            .Decorate(typeof(ICommandHandler<,>), typeof(LoggedCommandHandler<,>))
            .Decorate(typeof(IQueryHandler<,>), typeof(LoggedQueryHandler<,>));
    }

    internal static IServiceCollection AddGameTool<T>(this IServiceCollection serviceCollection)
        where T : class, IGameTool =>
        serviceCollection
            .AddScoped<T>()
            .AddScoped<AIFunction>(sp =>
                AIFunctionFactory.Create(
                    sp.GetRequiredService<T>().Invoke,
                    new AIFunctionFactoryOptions
                    {
                        SerializerOptions = TRPG.Contracts.TrpgJsonOptions.Default,
                    }
                )
            );
}
