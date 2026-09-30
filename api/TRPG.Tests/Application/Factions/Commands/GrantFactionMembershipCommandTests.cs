using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Factions.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Factions.Commands;

public sealed class GrantFactionMembershipCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private readonly Creature _creature = Builders.MakeCreature(WorldId);
    private readonly Faction _faction = Builders.MakeFaction(WorldId);
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _context.AddRange(_creature, _faction);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_GrantsMembershipOnlyOnce_WhenRetried()
    {
        var command = new GrantFactionMembershipCommand(WorldId, _creature.Id, _faction.Id);
        var handler = _serviceProvider.GetRequiredService<
            ICommandHandler<GrantFactionMembershipCommand>
        >();

        await handler.Handle(command, TestContext.Current.CancellationToken);
        await handler.Handle(command, TestContext.Current.CancellationToken);

        await using var verifyContext = db.CreateContext();
        var membership = await verifyContext.FactionMembers.SingleAsync(
            member => member.CreatureId == _creature.Id && member.FactionId == _faction.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(FactionRole.Member, membership.Role);
    }
}
