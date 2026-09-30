using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Factions.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Factions.Commands;

public sealed class ApplyTerminalChainStandingChangeCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private readonly Faction _giver = Builders.MakeFaction(WorldId);
    private readonly Faction _antagonist = Builders.MakeFaction(WorldId);
    private readonly Faction _other = Builders.MakeFaction(WorldId);
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _context.Factions.AddRange(_giver, _antagonist, _other);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_DeepensOnlyNegativeMutualStandings()
    {
        var giverStanding = MakeStanding(_giver.Id, _antagonist.Id, -20);
        var antagonistStanding = MakeStanding(_antagonist.Id, _giver.Id, -10);
        var neutralStanding = MakeStanding(_giver.Id, _other.Id, 0);
        _context.FactionStandings.AddRange(giverStanding, antagonistStanding, neutralStanding);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _serviceProvider
            .GetRequiredService<ICommandHandler<ApplyTerminalChainStandingChangeCommand>>()
            .Handle(
                new ApplyTerminalChainStandingChangeCommand(WorldId, _giver.Id, _antagonist.Id),
                TestContext.Current.CancellationToken
            );

        await using var verifyContext = db.CreateContext();
        var scores = await verifyContext.FactionStandings.ToDictionaryAsync(
            standing => standing.Id,
            standing => standing.Score,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(-25, scores[giverStanding.Id]);
        Assert.Equal(-15, scores[antagonistStanding.Id]);
        Assert.Equal(0, scores[neutralStanding.Id]);
    }

    private static FactionStanding MakeStanding(Guid factionId, Guid otherFactionId, int score) =>
        new()
        {
            WorldId = WorldId,
            FactionId = factionId,
            OtherFactionId = otherFactionId,
            Score = score,
        };
}
