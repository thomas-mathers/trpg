using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Abilities;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Queries;

public sealed class GetCreatureEffectAdvancesQueryTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly GameInstant OneRoundIn = GameClock.Epoch + CombatTiming.Round;

    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private IQueryHandler<
        GetCreatureEffectAdvancesQuery,
        IReadOnlyCollection<CreatureEffectAdvance>
    > _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<
            IQueryHandler<
                GetCreatureEffectAdvancesQuery,
                IReadOnlyCollection<CreatureEffectAdvance>
            >
        >();

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Creature> SeedBurning(
        Guid? locationId = null,
        CreatureCondition condition = CreatureCondition.Awake
    )
    {
        var creature = Builders.MakeCreature(
            _worldId,
            locationId: locationId ?? _locationId,
            currentHp: 50
        );
        creature.Condition = condition;
        creature.ActiveDots =
        [
            new ActiveDot
            {
                AbilityName = "Ignite",
                Amount = 5,
                DamageType = nameof(DamageType.Fire),
                NextTickAt = OneRoundIn,
                ExpiresAt = OneRoundIn + CombatTiming.Round * 10,
            },
        ];
        _context.Creatures.Add(creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return creature;
    }

    private Task<IReadOnlyCollection<CreatureEffectAdvance>> Query(
        IReadOnlyCollection<Guid>? excluded = null
    ) =>
        _handler.Handle(
            new GetCreatureEffectAdvancesQuery
            {
                WorldId = _worldId,
                LocationIds = [_locationId],
                GameTime = OneRoundIn,
                ExcludedCreatureIds = excluded ?? [],
            },
            TestContext.Current.CancellationToken
        );

    [Fact]
    public async Task Handle_ReturnsTheAdvancedVitals_ForACreatureWithADueEffect()
    {
        // Arrange
        var burning = await SeedBurning();

        // Act
        var advances = await Query();

        // Assert
        var advance = Assert.Single(advances);
        Assert.Equal((burning.Id, _locationId), (advance.Vitals.CreatureId, advance.LocationId));
        Assert.True(advance.Vitals.CurrentHp < 50);
    }

    [Fact]
    public async Task Handle_DoesNotWriteTheAdvancedState()
    {
        // Arrange
        var burning = await SeedBurning();

        // Act
        await Query();

        // Assert
        await using var verifyContext = db.CreateContext();
        var reloaded = await verifyContext.Creatures.FindAsync(
            [burning.Id],
            TestContext.Current.CancellationToken
        );
        Assert.Equal(50, reloaded!.CurrentHp);
    }

    [Fact]
    public async Task Handle_SkipsCreaturesAtOtherLocations()
    {
        // Arrange
        await SeedBurning(locationId: Guid.NewGuid());

        // Act
        var advances = await Query();

        // Assert
        Assert.Empty(advances);
    }

    [Fact]
    public async Task Handle_SkipsExcludedAndDeadCreatures()
    {
        // Arrange
        var excluded = await SeedBurning();
        await SeedBurning(condition: CreatureCondition.Dead);

        // Act
        var advances = await Query(excluded: [excluded.Id]);

        // Assert
        Assert.Empty(advances);
    }
}
