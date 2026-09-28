using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Chat.Queries;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Chat.Queries;

public sealed class GetNextChatMessageOrdinalQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private GetNextChatMessageOrdinalQueryHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<GetNextChatMessageOrdinalQueryHandler>();

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsZero_WhenTheSessionHasNoMessages()
    {
        // Act
        var result = await _handler.Handle(
            new GetNextChatMessageOrdinalQuery { SessionId = Guid.NewGuid() },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public async Task Handle_ReturnsOneGreaterThanTheHighestOrdinal_WhenMessagesExist()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        _context.ChatMessages.AddRange(
            Builders.MakeChatMessage(sessionId, ordinal: 0),
            Builders.MakeChatMessage(sessionId, ordinal: 1),
            Builders.MakeChatMessage(sessionId, ordinal: 2)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetNextChatMessageOrdinalQuery { SessionId = sessionId },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(3, result);
    }
}
