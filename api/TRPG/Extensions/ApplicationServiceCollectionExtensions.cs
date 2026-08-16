using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Abilities.Queries;
using TRPG.Application.Buildings.Commands;
using TRPG.Application.Buildings.Queries;
using TRPG.Application.Chat.Commands;
using TRPG.Application.Chat.Queries;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Extensions;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Common.Validation;
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
using TRPG.Application.GameTurns.Extensions;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Narration.Queries;
using TRPG.Application.NpcConversations.Commands;
using TRPG.Application.NpcConversations.Queries;
using TRPG.Application.Quests;
using TRPG.Application.Quests.Commands;
using TRPG.Application.Quests.Extensions;
using TRPG.Application.Quests.Queries;
using TRPG.Application.Reputations.Commands;
using TRPG.Application.Reputations.Queries;
using TRPG.Application.Scenes;
using TRPG.Application.Scenes.Commands;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Trading;
using TRPG.Application.Trading.Extensions;
using TRPG.Application.WeaponProficiency.Commands;
using TRPG.Application.WeaponProficiency.Queries;
using TRPG.Application.Worlds;
using TRPG.Application.Worlds.Extensions;
using TRPG.Application.Worlds.Queries;
using TRPG.Commands;
using TRPG.GameTurns.Tools;
using TRPG.Queries;
using TRPG.Tools;
using TRPG.Validation;

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
            .AddQuestServices()
            .AddWorldsServices()
            .AddCombatServices()
            .AddGameTurnsServices()
            .AddGameTool<LookTool>()
            .AddGameTool<MoveTool>()
            .AddGameTool<CreatureInspectTool>()
            .AddGameTool<LookupTool>()
            .Decorate(typeof(ICommandHandler<>), typeof(ValidatingCommandHandlerDecorator<>))
            .Decorate(typeof(ICommandHandler<,>), typeof(ValidatingCommandHandlerDecorator<,>))
            .Decorate(typeof(ICommandHandler<>), typeof(LoggedCommandHandlerDecorator<>))
            .Decorate(typeof(ICommandHandler<,>), typeof(LoggedCommandHandlerDecorator<,>))
            .Decorate(typeof(IQueryHandler<,>), typeof(LoggedQueryHandlerDecorator<,>));
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
                        SerializerOptions = TRPG.Application
                            .Common
                            .Serialization
                            .TrpgJsonOptions
                            .Default,
                    }
                )
            );
}
