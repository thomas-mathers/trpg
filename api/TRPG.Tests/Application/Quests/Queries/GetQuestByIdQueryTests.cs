using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Queries;
using TRPG.Application.Quests.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Quests.Queries;

public sealed class GetQuestByIdQueryTests(DatabaseFixture database)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private IQueryHandler<GetQuestByIdQuery, Quest?> _handler = null!;
    private readonly Quest _quest = Builders.MakeQuest(Guid.NewGuid(), WorldId);

    public async ValueTask InitializeAsync()
    {
        _context = database.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<IQueryHandler<GetQuestByIdQuery, Quest?>>();

        _context.Quests.Add(_quest);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsTheQuest_WhenItExists()
    {
        // Act
        var result = await _handler.Handle(
            new GetQuestByIdQuery { Id = _quest.Id },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_quest.Name, result.Name);
        Assert.Equal(_quest.GiverId, result.GiverId);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenTheQuestDoesNotExist()
    {
        // Act
        var result = await _handler.Handle(
            new GetQuestByIdQuery { Id = Guid.NewGuid() },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Null(result);
    }
}
