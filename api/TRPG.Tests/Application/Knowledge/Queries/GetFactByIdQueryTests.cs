using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Queries;
using TRPG.Application.Knowledge.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Knowledge.Queries;

public sealed class GetFactByIdQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly World _world = Builders.MakeWorld();
    private Fact _fact = null!;
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _fact = new Fact
        {
            WorldId = _world.Id,
            Subject = "the old road",
            Value = "is flooded",
        };
        _context.AddRange(_world, _fact);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsTheRequestedFact()
    {
        var result = await Handler()
            .Handle(
                new GetFactByIdQuery { FactId = _fact.Id },
                TestContext.Current.CancellationToken
            );

        Assert.NotNull(result);
        Assert.Equal(_fact.Subject, result.Subject);
        Assert.Equal(_fact.Value, result.Value);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenTheFactDoesNotExist()
    {
        var result = await Handler()
            .Handle(
                new GetFactByIdQuery { FactId = Guid.NewGuid() },
                TestContext.Current.CancellationToken
            );

        Assert.Null(result);
    }

    private IQueryHandler<GetFactByIdQuery, Fact?> Handler() =>
        _serviceProvider.GetRequiredService<IQueryHandler<GetFactByIdQuery, Fact?>>();
}
