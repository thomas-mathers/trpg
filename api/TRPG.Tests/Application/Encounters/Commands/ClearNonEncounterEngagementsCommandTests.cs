using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.Encounters.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

public sealed class ClearNonEncounterEngagementsCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();

        var locationId = Guid.NewGuid();
        var player = Builders.MakeCreature(_worldId, locationId: locationId);
        var guard = Builders.MakeCreature(_worldId, locationId: locationId);
        var bystander = Builders.MakeCreature(_worldId, locationId: locationId);
        player.IsEngaged = true;
        guard.IsEngaged = true;
        bystander.IsEngaged = true;
        _context.Creatures.AddRange(player, guard, bystander);
        _context.Encounters.Add(
            Builders.MakeGuardEncounter(_worldId, player.Id, locationId, guard.Id, fineAmount: 100)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReleasesOnlyCreaturesOutsideActiveEncounters()
    {
        var handler = _serviceProvider.GetRequiredService<
            ICommandHandler<ClearNonEncounterEngagementsCommand>
        >();

        await handler.Handle(
            new ClearNonEncounterEngagementsCommand
            {
                WorldId = _worldId,
                GameTime = GameClock.Epoch + TimeSpan.FromHours(2),
            },
            TestContext.Current.CancellationToken
        );

        _context.ChangeTracker.Clear();
        var creatures = await _context.Creatures.ToArrayAsync(
            TestContext.Current.CancellationToken
        );
        var activeEncounter = await _context.Encounters.SingleAsync(
            TestContext.Current.CancellationToken
        );
        var participantIds = new[]
        {
            activeEncounter.PlayerId,
            ((GuardEncounter)activeEncounter).GuardCreatureId,
        };
        Assert.All(
            creatures.Where(creature => participantIds.Contains(creature.Id)),
            creature => Assert.True(creature.IsEngaged)
        );
        Assert.False(creatures.Single(creature => !participantIds.Contains(creature.Id)).IsEngaged);
    }
}
