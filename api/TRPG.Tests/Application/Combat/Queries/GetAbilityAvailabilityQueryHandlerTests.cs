using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Abilities;
using TRPG.Application.Combat.Queries;
using TRPG.Application.Common.Queries;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Combat.Queries;

public sealed class GetAbilityAvailabilityQueryHandlerTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Creature> SeedPlayer(int currentMp)
    {
        var player = Builders.MakeCreature(_worldId, currentMp: currentMp);
        _context.Creatures.Add(player);
        _context.CreatureSkills.Add(
            Builders.MakeCreatureSkill(
                player.Id,
                Skill.Restoration,
                level: 1,
                experience: 10_000,
                worldId: _worldId
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return player;
    }

    private async Task<AbilityAvailability> GetMend(Guid playerId)
    {
        var handler = _serviceProvider.GetRequiredService<
            IQueryHandler<GetAbilityAvailabilityQuery, IReadOnlyList<AbilityAvailability>>
        >();
        var availability = await handler.Handle(
            new GetAbilityAvailabilityQuery { PlayerId = playerId },
            TestContext.Current.CancellationToken
        );
        return availability.Single(a => a.Name == "Mend");
    }

    [Fact]
    public async Task Handle_ReportsAbilitiesUsable_WithoutAnActiveFight()
    {
        // Arrange
        var player = await SeedPlayer(currentMp: 8);

        // Act
        var mend = await GetMend(player.Id);

        // Assert
        Assert.True(mend.IsUsable);
    }

    [Fact]
    public async Task Handle_ReportsNotEnoughMp_WhenThePlayerCannotAffordTheAbility()
    {
        // Arrange
        var player = await SeedPlayer(currentMp: 0);

        // Act
        var mend = await GetMend(player.Id);

        // Assert
        Assert.Equal("not enough MP", mend.Reason);
    }
}
