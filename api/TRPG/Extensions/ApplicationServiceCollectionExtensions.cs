using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Chat.Extensions;
using TRPG.Application.Combat.Extensions;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Common.Validation;
using TRPG.Application.Creatures.Extensions;
using TRPG.Application.Crimes.Extensions;
using TRPG.Application.Encounters.Extensions;
using TRPG.Application.GameTurns;
using TRPG.Application.GameTurns.Extensions;
using TRPG.Application.Inventory.Extensions;
using TRPG.Application.LocationSimulation.Extensions;
using TRPG.Application.NpcConversations.Extensions;
using TRPG.Application.Quests.Extensions;
using TRPG.Application.Reputations.Extensions;
using TRPG.Application.WorldGeneration.Extensions;
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
            .AddChatServices()
            .AddNpcConversationsServices()
            .AddInventoryServices()
            .AddCreaturesServices()
            .AddCrimesServices()
            .AddEncountersServices()
            .AddLocationSimulationServices()
            .AddQuestServices()
            .AddReputationsServices()
            .AddWorldGenerationServices()
            .AddCombatServices()
            .AddGameTurnsServices()
            .AddGameTool<LookTool>()
            .AddGameTool<MoveTool>()
            .AddGameTool<LockpickTool>()
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
