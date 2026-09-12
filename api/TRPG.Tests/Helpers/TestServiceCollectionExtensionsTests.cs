using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Configuration;
using TRPG.Application.GameTurns;
using TRPG.Data;

namespace TRPG.Tests.Helpers;

public sealed class TestServiceCollectionExtensionsTests
{
    [Fact]
    public async Task AddTrpgTestServices_KeepsRuntimeStateIsolated_WhenRegistrationsAreReused()
    {
        // Arrange
        await using var firstContext = new TrpgDbContext(new DbContextOptions<TrpgDbContext>());
        await using var secondContext = new TrpgDbContext(new DbContextOptions<TrpgDbContext>());
        await using var first = new ServiceCollection()
            .AddTrpgTestServices(firstContext)
            .BuildServiceProvider();
        var playerId = Guid.NewGuid();
        first.GetRequiredService<GameTurnContext>().PlayerId = playerId;
        first.GetRequiredService<IMemoryCache>().Set("player", playerId);
        var firstChatClient = first.GetRequiredKeyedService<IChatClient>(LlmRoleKeys.Gameplay);

        // Act
        await using var second = new ServiceCollection()
            .AddTrpgTestServices(secondContext)
            .BuildServiceProvider();

        // Assert
        Assert.NotEqual(playerId, second.GetRequiredService<GameTurnContext>().PlayerId);
        Assert.False(second.GetRequiredService<IMemoryCache>().TryGetValue("player", out _));
        Assert.NotSame(
            firstChatClient,
            second.GetRequiredKeyedService<IChatClient>(LlmRoleKeys.Gameplay)
        );
        Assert.Same(firstContext, first.GetRequiredService<TrpgDbContext>());
        Assert.Same(secondContext, second.GetRequiredService<TrpgDbContext>());
    }
}
