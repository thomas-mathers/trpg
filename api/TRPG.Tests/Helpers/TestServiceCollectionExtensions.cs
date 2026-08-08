using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Extensions;
using TRPG.Application.GameSessions;
using TRPG.Data;

namespace TRPG.Tests.Helpers;

internal static class TestServiceCollectionExtensions
{
    public static IServiceCollection AddTrpgTestServices(
        this IServiceCollection services,
        TrpgDbContext context
    ) =>
        services
            .AddTrpgApplicationServices()
            .AddScoped<TestGameClientEventPublisher>()
            .AddScoped<IGameClientEventPublisher>(sp =>
                sp.GetRequiredService<TestGameClientEventPublisher>()
            )
            .AddSingleton(context)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .AddSingleton(typeof(IOptionsSnapshot<>), typeof(DefaultOptionsSnapshot<>));
}

internal sealed class TestGameClientEventPublisher : IGameClientEventPublisher
{
    public List<GameTurnEvent> PublishedEvents { get; } = [];

    public void Publish(GameTurnEvent gameEvent) => PublishedEvents.Add(gameEvent);
}
