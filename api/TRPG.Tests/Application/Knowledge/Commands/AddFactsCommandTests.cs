using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Knowledge.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Knowledge.Commands;

public sealed class AddFactsCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly World _world = Builders.MakeWorld();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _context.Worlds.Add(_world);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_PersistsFactsOwnedByKnowledge()
    {
        var facts = new[]
        {
            new Fact
            {
                WorldId = _world.Id,
                Subject = "the old road",
                Value = "is flooded",
            },
            new Fact
            {
                WorldId = _world.Id,
                Subject = "the east gate",
                Value = "opens at dawn",
            },
        };

        await _serviceProvider
            .GetRequiredService<ICommandHandler<AddFactsCommand>>()
            .Handle(new AddFactsCommand { Facts = facts }, TestContext.Current.CancellationToken);

        await using var verifyContext = db.CreateContext();
        var stored = await verifyContext
            .Facts.Where(fact => fact.WorldId == _world.Id)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(facts.Select(fact => fact.Id).Order(), stored.Select(fact => fact.Id).Order());
    }
}
