using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Chat.Commands;
using TRPG.Data;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Chat.Commands;

public sealed class RemoveChatMessagesFromOrdinalCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _sessionId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private RemoveChatMessagesFromOrdinalCommandHandler _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<RemoveChatMessagesFromOrdinalCommandHandler>();

        _context.ChatMessages.AddRange(
            Builders.MakeChatMessage(_sessionId, ordinal: 0),
            Builders.MakeChatMessage(_sessionId, ordinal: 1),
            Builders.MakeChatMessage(_sessionId, ordinal: 2),
            Builders.MakeChatMessage(_sessionId, ordinal: 3)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_RemovesMessagesAtOrAfterTheGivenOrdinal_AndKeepsEarlierOnes()
    {
        // Act
        await _handler.Handle(
            new RemoveChatMessagesFromOrdinalCommand { SessionId = _sessionId, FromOrdinal = 2 },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var remainingOrdinals = await verifyContext
            .ChatMessages.Where(m => m.SessionId == _sessionId)
            .Select(m => m.Ordinal)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal([0, 1], remainingOrdinals.OrderBy(ordinal => ordinal));
    }
}
