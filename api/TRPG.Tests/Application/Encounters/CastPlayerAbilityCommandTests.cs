using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Abilities;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Configuration;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters;

public sealed class CastPlayerAbilityCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Point PlayerPosition = new(0, 0);
    private static readonly Point InRange = new(0, -2);
    private static readonly Point OutOfRange = new(0, -30);

    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _stateId = Guid.NewGuid();
    private readonly Guid _locationId = Guid.NewGuid();

    private readonly List<CreatureKilledEvent> _kills = [];

    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ICommandHandler<CastPlayerAbilityCommand, AbilityCastResult> _handler = null!;

    public ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IDomainEventPublisher<CreatureKilledEvent>>(
                new RecordingKillPublisher(_kills)
            )
            .AddSingleton<IOptionsSnapshot<CombatOptions>>(
                new TestOptionsSnapshot<CombatOptions>(
                    new CombatOptions
                    {
                        MinHitChance = 1.0f,
                        MaxHitChance = 1.0f,
                        CritChancePerDexterityPoint = 0f,
                    }
                )
            )
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<
            ICommandHandler<CastPlayerAbilityCommand, AbilityCastResult>
        >();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    private async Task<Creature> SeedKnight()
    {
        var generated = Builders
            .MakeCreatureGenerator()
            .Generate(
                new CreatureGeneratorInput(
                    CreatureType.Human,
                    CreatureArchetype.For(Profession.Knight),
                    _worldId,
                    _locationId,
                    MinLevel: 1,
                    MaxLevel: 1
                )
            );
        generated.Creature.LocationId = _locationId;
        _context.Creatures.Add(generated.Creature);
        _context.Items.AddRange(generated.Items);
        _context.CreatureSkills.AddRange(generated.Skills);
        await SeedWorld(generated.Creature.Id);
        return generated.Creature;
    }

    private async Task<Creature> SeedHealer()
    {
        var healer = Builders.MakeCreature(
            _worldId,
            locationId: _locationId,
            name: "Hero",
            currentHp: 20,
            currentMp: 8
        );
        _context.Creatures.Add(healer);
        _context.CreatureSkills.Add(
            Builders.MakeCreatureSkill(
                healer.Id,
                Skill.Restoration,
                level: 1,
                experience: 10_000,
                worldId: _worldId
            )
        );
        await SeedWorld(healer.Id);
        return healer;
    }

    private async Task SeedWorld(Guid playerId)
    {
        _context.Worlds.Add(Builders.MakeWorld(_worldId));
        _context.States.Add(Builders.MakeState(Guid.NewGuid(), worldId: _worldId, id: _stateId));
        _context.Locations.Add(Builders.MakeLocation(_worldId, _stateId, id: _locationId));
        _context.GameSessions.Add(Builders.MakeGameSession(_worldId, playerId));
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _context.ChangeTracker.Clear();
    }

    private async Task<Creature> SeedTarget(
        string name,
        int? currentHp = null,
        Guid? locationId = null
    )
    {
        var target = Builders.MakeCreature(
            _worldId,
            creatureType: CreatureType.Beast,
            locationId: locationId ?? _locationId,
            name: name,
            currentHp: currentHp
        );
        _context.Creatures.Add(target);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _context.ChangeTracker.Clear();
        return target;
    }

    private CastPlayerAbilityCommand Cast(
        Creature player,
        Guid targetId,
        string abilityName,
        Point targetPosition,
        ActiveFightHolder? held = null
    ) =>
        new()
        {
            WorldId = _worldId,
            PlayerId = player.Id,
            TargetId = targetId,
            AbilityName = abilityName,
            GameTime = held?.At ?? TestTime.Start,
            Fight = held?.Fight,
            PlayerPlacement = new PlayerPlacement(PlayerPosition, 0),
            FindLiveCreaturePosition = _ => targetPosition,
        };

    private Task<bool> IsEngaged(Guid creatureId) =>
        _context
            .Creatures.AsNoTracking()
            .Where(creature => creature.Id == creatureId)
            .Select(creature => creature.IsEngaged)
            .SingleAsync(TestContext.Current.CancellationToken);

    [Fact]
    public async Task Handle_ReportsAbilityNotFound_WhenThePlayerLacksTheAbility()
    {
        // Arrange
        var player = await SeedKnight();
        var target = await SeedTarget("Wolf");

        // Act
        var result = await _handler.Handle(
            Cast(player, target.Id, "Nonexistent", InRange),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(result.AbilityNotFound);
    }

    [Fact]
    public async Task Handle_Succeeds_WhenASupportAbilityTargetsAnotherCreature()
    {
        // Arrange
        var player = await SeedHealer();
        var target = await SeedTarget("Bystander", currentHp: 5);

        // Act
        var result = await _handler.Handle(
            Cast(player, target.Id, "Mend", InRange),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(result.Succeeded, result.Message);
    }

    [Fact]
    public async Task Handle_HoldsTheFight_WhenAnAttackAbilityOpensOne()
    {
        // Arrange
        var player = await SeedKnight();
        var target = await SeedTarget("Wolf", currentHp: 1000);

        // Act
        var result = await _handler.Handle(
            Cast(player, target.Id, "Strike", InRange),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotNull(result.Fight);
    }

    [Fact]
    public async Task Handle_EngagesTheEnemiesButNotThePlayer_WhenAFightOpens()
    {
        // Arrange
        var player = await SeedKnight();
        var target = await SeedTarget("Wolf", currentHp: 1000);

        // Act
        await _handler.Handle(
            Cast(player, target.Id, "Strike", InRange),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(await IsEngaged(target.Id));
        Assert.False(await IsEngaged(player.Id));
    }

    [Fact]
    public async Task Handle_HoldsNoFight_WhenTheOpeningStrikeKills()
    {
        // Arrange
        var player = await SeedKnight();
        var target = await SeedTarget("Wolf", currentHp: 1);

        // Act
        var result = await _handler.Handle(
            Cast(player, target.Id, "Strike", InRange),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(result.Succeeded);
        Assert.Null(result.Fight);
    }

    [Fact]
    public async Task Handle_StartsNoFight_WhenTheTargetIsOutOfRange()
    {
        // Arrange
        var player = await SeedKnight();
        var target = await SeedTarget("Wolf", currentHp: 1000);

        // Act
        var result = await _handler.Handle(
            Cast(player, target.Id, "Strike", OutOfRange),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result.Succeeded);
        Assert.Null(result.Fight);
        Assert.False(await IsEngaged(target.Id));
    }

    [Fact]
    public async Task Handle_Refuses_WhenTheTargetIsNotHere()
    {
        // Arrange
        var player = await SeedKnight();
        var faraway = await SeedTarget("Distant Wolf", locationId: Guid.NewGuid());

        // Act
        var result = await _handler.Handle(
            Cast(player, faraway.Id, "Strike", InRange),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Handle_KeepsTheHeldFight_WhenALaterCastIsRefused()
    {
        // Arrange
        var player = await SeedKnight();
        var target = await SeedTarget("Wolf", currentHp: 1000);
        var opened = await _handler.Handle(
            Cast(player, target.Id, "Strike", InRange),
            TestContext.Current.CancellationToken
        );
        var held = new ActiveFightHolder(opened.Fight, TestTime.Start + TimeSpan.FromSeconds(1));

        // Act
        var result = await _handler.Handle(
            Cast(player, target.Id, "Strike", InRange, held),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result.Succeeded);
        Assert.Same(opened.Fight, result.Fight);
    }

    [Fact]
    public async Task Handle_PersistsTheDamage_WhenACastLandsInAHeldFight()
    {
        // Arrange
        var player = await SeedKnight();
        var target = await SeedTarget("Wolf", currentHp: 1000);
        var opened = await _handler.Handle(
            Cast(player, target.Id, "Strike", InRange),
            TestContext.Current.CancellationToken
        );
        var hpAfterOpening = await CurrentHp(target.Id);
        var held = new ActiveFightHolder(
            opened.Fight,
            TestTime.Start + CombatTiming.GlobalCooldown
        );

        // Act
        await _handler.Handle(
            Cast(player, target.Id, "Strike", InRange, held),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(await CurrentHp(target.Id) < hpAfterOpening);
    }

    [Fact]
    public async Task Handle_ReportsEachKillOnce_WhenALaterCastLandsAfterAnEnemyDied()
    {
        // Arrange
        var player = await SeedKnight();
        var fallen = await SeedTarget("Wolf", currentHp: 1);
        var survivor = await SeedTarget("Wolf", currentHp: 1000);
        await SeedGroup(fallen.Id, survivor.Id);
        var opened = await _handler.Handle(
            Cast(player, fallen.Id, "Strike", InRange),
            TestContext.Current.CancellationToken
        );
        var held = new ActiveFightHolder(
            opened.Fight,
            TestTime.Start + CombatTiming.GlobalCooldown
        );

        // Act
        await _handler.Handle(
            Cast(player, survivor.Id, "Strike", InRange, held),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Single(_kills, kill => kill.CreatureId == fallen.Id);
    }

    [Fact]
    public async Task Handle_CreditsEachSwingOnce_WhenSeveralCastsLandInAHeldFight()
    {
        // Arrange
        var player = await SeedKnight();
        var target = await SeedTarget("Wolf", currentHp: 1000);
        var opened = await _handler.Handle(
            Cast(player, target.Id, "Strike", InRange),
            TestContext.Current.CancellationToken
        );
        var proficiencyAfterOpening = await TotalProficiency(player.Id);
        var held = new ActiveFightHolder(
            opened.Fight,
            TestTime.Start + CombatTiming.GlobalCooldown
        );

        // Act
        await _handler.Handle(
            Cast(player, target.Id, "Strike", InRange, held),
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(proficiencyAfterOpening * 2, await TotalProficiency(player.Id));
    }

    private async Task<int> TotalProficiency(Guid creatureId) =>
        await _context
            .CreatureWeaponProficiencies.AsNoTracking()
            .Where(proficiency => proficiency.CreatureId == creatureId)
            .SumAsync(
                proficiency => proficiency.Proficiency,
                TestContext.Current.CancellationToken
            );

    private async Task SeedGroup(params Guid[] creatureIds)
    {
        var group = Builders.MakeEncounterGroup(_worldId, _locationId, Guid.NewGuid());
        _context.EncounterGroups.Add(group);
        _context.EncounterGroupMembers.AddRange(
            creatureIds.Select(id => Builders.MakeEncounterGroupMember(_worldId, group.Id, id))
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _context.ChangeTracker.Clear();
    }

    private sealed class RecordingKillPublisher(List<CreatureKilledEvent> kills)
        : IDomainEventPublisher<CreatureKilledEvent>
    {
        public Task Publish(
            CreatureKilledEvent domainEvent,
            CancellationToken cancellationToken = default
        )
        {
            kills.Add(domainEvent);
            return Task.CompletedTask;
        }
    }

    private Task<int> CurrentHp(Guid creatureId) =>
        _context
            .Creatures.AsNoTracking()
            .Where(creature => creature.Id == creatureId)
            .Select(creature => creature.CurrentHp)
            .SingleAsync(TestContext.Current.CancellationToken);

    private sealed record ActiveFightHolder(
        TRPG.Application.Encounters.Fights.ActiveFight? Fight,
        GameInstant At
    );
}
