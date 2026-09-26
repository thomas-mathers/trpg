using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

public sealed class RegenerateCreaturesAtLocationCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();
    private static readonly GameInstant TwelveSecondsIn =
        GameClock.Epoch + TimeSpan.FromSeconds(12);

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private RegenerateCreaturesAtLocationCommandHandler _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IOptionsSnapshot<CreatureRegenOptions>>(
                new TestOptionsSnapshot<CreatureRegenOptions>(
                    new CreatureRegenOptions
                    {
                        HpRegenPercentPerTick = 0.2f,
                        ApRegenPercentPerTick = 0.25f,
                        MpRegenPercentPerTick = 0.25f,
                    }
                )
            )
            .BuildServiceProvider();
        _handler =
            _serviceProvider.GetRequiredService<RegenerateCreaturesAtLocationCommandHandler>();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private static RegenerateCreaturesAtLocationCommand MakeCommand(
        Guid locationId,
        IReadOnlyCollection<Guid>? excludedCreatureIds = null
    ) =>
        new()
        {
            WorldId = WorldId,
            LocationId = locationId,
            GameTime = TwelveSecondsIn,
            ExcludedCreatureIds = excludedCreatureIds ?? [],
        };

    private async Task<Creature> SeedInjuredCreature(Guid locationId, CreatureState state = default)
    {
        var creature = Builders.MakeCreature(
            WorldId,
            locationId: locationId,
            currentHp: 0,
            currentAp: 0,
            currentMp: 0,
            state: state
        );
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return creature;
    }

    [Fact]
    public async Task Handle_RegeneratesWholeTicksAndReturnsChangedVitals_WhenCreatureIsBelowMaximum()
    {
        // Arrange
        var locationId = Guid.NewGuid();
        var creature = await SeedInjuredCreature(locationId);

        // Act
        var result = await _handler.Handle(
            MakeCommand(locationId),
            TestContext.Current.CancellationToken
        );

        // Assert
        var vitals = Assert.Single(result);
        Assert.Equal(creature.Id, vitals.CreatureId);
        Assert.Equal(14, vitals.CurrentHp);
        Assert.Equal(6, vitals.CurrentAp);
        Assert.Equal(4, vitals.CurrentMp);
    }

    [Fact]
    public async Task Handle_PersistsRegeneratedResourcesAndBanksPartialTick_WhenCreatureIsBelowMaximum()
    {
        // Arrange
        var locationId = Guid.NewGuid();
        var creature = await SeedInjuredCreature(locationId);

        // Act
        await _handler.Handle(MakeCommand(locationId), TestContext.Current.CancellationToken);

        // Assert
        await using var verifyContext = db.CreateContext();
        var persisted = await verifyContext.Creatures.FindAsync(
            [creature.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(14, persisted!.CurrentHp);
        Assert.Equal(GameClock.Epoch + TimeSpan.FromSeconds(10), persisted.LastRegenGameTime);
    }

    [Fact]
    public async Task Handle_ReturnsNothing_WhenCreatureIsAtFullResources()
    {
        // Arrange
        var locationId = Guid.NewGuid();
        _context.Creatures.Add(Builders.MakeCreature(WorldId, locationId: locationId));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            MakeCommand(locationId),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_ReturnsNothing_WhenCreatureIsDead()
    {
        // Arrange
        var locationId = Guid.NewGuid();
        await SeedInjuredCreature(locationId, CreatureState.Dead);

        // Act
        var result = await _handler.Handle(
            MakeCommand(locationId),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_ReturnsNothing_WhenCreatureIsAtAnotherLocation()
    {
        // Arrange
        await SeedInjuredCreature(Guid.NewGuid());

        // Act
        var result = await _handler.Handle(
            MakeCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task Handle_LeavesExcludedCreatureUntouched_WhenItIsListedAsExcluded()
    {
        // Arrange
        var locationId = Guid.NewGuid();
        var excluded = await SeedInjuredCreature(locationId);
        var regenerating = await SeedInjuredCreature(locationId);

        // Act
        var result = await _handler.Handle(
            MakeCommand(locationId, excludedCreatureIds: [excluded.Id]),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(regenerating.Id, Assert.Single(result).CreatureId);
        await using var verifyContext = db.CreateContext();
        var persisted = await verifyContext.Creatures.FindAsync(
            [excluded.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(0, persisted!.CurrentHp);
        Assert.Equal(GameClock.Epoch, persisted.LastRegenGameTime);
    }

    [Fact]
    public async Task Handle_ReturnsNothing_WhenLessThanOneTickHasElapsed()
    {
        // Arrange
        var locationId = Guid.NewGuid();
        await SeedInjuredCreature(locationId);

        // Act
        var result = await _handler.Handle(
            new RegenerateCreaturesAtLocationCommand
            {
                WorldId = WorldId,
                LocationId = locationId,
                GameTime = GameClock.Epoch + TimeSpan.FromSeconds(4),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Empty(result);
    }
}
