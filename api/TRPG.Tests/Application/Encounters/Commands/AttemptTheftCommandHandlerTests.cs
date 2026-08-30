using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures;
using TRPG.Application.Encounters;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Inventory;
using TRPG.Application.Reputations.Events;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Encounters.Commands;

[Collection("Database")]
public sealed class AttemptTheftCommandHandlerTests(DatabaseFixture db) : IAsyncLifetime
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly Guid _theftLocationId = Guid.NewGuid();
    private readonly TestChanceRoller _chanceRoller = new();
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private AttemptTheftCommandHandler _handler = null!;
    private Creature _player = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _player = Builders.MakeCreature(WorldId, locationId: _theftLocationId);
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton<IOptionsMonitor<TheftOptions>>(
                new TestOptionsMonitor<TheftOptions>(
                    new TheftOptions
                    {
                        BaseDetectionChance = 1,
                        MaximumDetectionChance = 1,
                        MinimumDetectionChance = 1,
                    }
                )
            )
            .AddSingleton<IChanceRoller>(_chanceRoller)
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<AttemptTheftCommandHandler>();

        _context.Creatures.Add(_player);
        _context.CreatureSkills.AddRange(
            new CreatureSkill
            {
                WorldId = WorldId,
                CreatureId = _player.Id,
                Skill = Skill.Sneak,
                Level = 1,
            },
            new CreatureSkill
            {
                WorldId = WorldId,
                CreatureId = _player.Id,
                Skill = Skill.Pickpocketing,
                Level = 1,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsNotTheft_WhenNoItemsAreSelected()
    {
        var result = await _handler.Handle(
            new AttemptTheftCommand
            {
                From = new ItemOwnerReference(_player.Id, OwnerType.Creature),
                Items = [],
                PlayerId = _player.Id,
                WorldId = WorldId,
            },
            TestContext.Current.CancellationToken
        );

        Assert.Equal(TheftAttemptOutcome.NotTheft, result.Outcome);
    }

    [Fact]
    public async Task Handle_MovesOwnedContainerItemWithoutACheck_WhenNoOneCanWitnessTheTheft()
    {
        // Arrange
        var owner = Builders.MakeCreature(WorldId, locationId: Guid.NewGuid());
        var container = Builders.MakeContainer(WorldId, _theftLocationId);
        container.OwnerCreatureId = owner.Id;
        var item = await SeedItem(container.Id, OwnerType.Container);
        _context.Creatures.Add(owner);
        _context.Props.Add(container);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await AttemptTheft(
            new ItemOwnerReference(container.Id, OwnerType.Container),
            item
        );

        // Assert
        Assert.Equal(TheftAttemptOutcome.Completed, result.Outcome);
        await using var verifyContext = db.CreateContext();
        var movedItem = await verifyContext.Items.SingleAsync(
            candidate => candidate.Id == item.Id,
            TestContext.Current.CancellationToken
        );
        var crime = await verifyContext
            .Crimes.OfType<TheftCrime>()
            .SingleAsync(
                candidate => candidate.PlayerId == _player.Id,
                TestContext.Current.CancellationToken
            );
        var sneakExperience = await verifyContext
            .CreatureSkills.Where(candidate =>
                candidate.CreatureId == _player.Id && candidate.Skill == Skill.Sneak
            )
            .Select(candidate => candidate.Experience)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(_player.Id, movedItem.Ownership.OwnerId);
        Assert.Equal(OwnerType.Creature, movedItem.Ownership.OwnerType);
        Assert.Equal(_theftLocationId, crime.LocationId);
        Assert.Equal(TheftCrimeOutcome.Taken, crime.Outcome);
        Assert.Empty(
            await verifyContext
                .CrimeWitnesses.Where(candidate => candidate.CrimeId == crime.Id)
                .ToArrayAsync(TestContext.Current.CancellationToken)
        );
        Assert.False(
            await verifyContext
                .Encounters.OfType<TheftEncounter>()
                .AnyAsync(
                    candidate => candidate.PlayerId == _player.Id,
                    TestContext.Current.CancellationToken
                )
        );
        Assert.Equal(0, sneakExperience);
    }

    [Fact]
    public async Task Handle_MovesPickpocketedItemAndAwardsPickpocketingExperience_WhenUndetected()
    {
        var owner = Builders.MakeCreature(WorldId, locationId: _theftLocationId);
        var item = await SeedItem(owner.Id, OwnerType.Creature);
        _context.Creatures.Add(owner);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _chanceRoller.Result = false;

        var result = await AttemptTheft(new ItemOwnerReference(owner.Id, OwnerType.Creature), item);

        Assert.Equal(TheftAttemptOutcome.Completed, result.Outcome);
        await using var verifyContext = db.CreateContext();
        var movedItem = await verifyContext.Items.FindAsync(
            [item.Id],
            TestContext.Current.CancellationToken
        );
        var experience = await verifyContext
            .CreatureSkills.Where(skill =>
                skill.CreatureId == _player.Id && skill.Skill == Skill.Pickpocketing
            )
            .Select(skill => skill.Experience)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(_player.Id, movedItem!.Ownership.OwnerId);
        Assert.Equal(10, experience);
        Assert.Empty(
            await verifyContext
                .CrimeWitnesses.Where(witness => witness.CreatureId == owner.Id)
                .ToArrayAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_MovesContainerItemAndAwardsSneakExperience_WhenWitnessDoesNotDetectTheft()
    {
        var owner = Builders.MakeCreature(WorldId, locationId: Guid.NewGuid());
        var witness = Builders.MakeCreature(WorldId, locationId: _theftLocationId);
        var container = Builders.MakeContainer(WorldId, _theftLocationId);
        container.OwnerCreatureId = owner.Id;
        var item = await SeedItem(container.Id, OwnerType.Container);
        _context.Creatures.AddRange(owner, witness);
        _context.Props.Add(container);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _chanceRoller.Result = false;

        var result = await AttemptTheft(
            new ItemOwnerReference(container.Id, OwnerType.Container),
            item
        );

        Assert.Equal(TheftAttemptOutcome.Completed, result.Outcome);
        await using var verifyContext = db.CreateContext();
        var movedItem = await verifyContext.Items.FindAsync(
            [item.Id],
            TestContext.Current.CancellationToken
        );
        var experience = await verifyContext
            .CreatureSkills.Where(skill =>
                skill.CreatureId == _player.Id && skill.Skill == Skill.Sneak
            )
            .Select(skill => skill.Experience)
            .SingleAsync(TestContext.Current.CancellationToken);

        Assert.Equal(_player.Id, movedItem!.Ownership.OwnerId);
        Assert.Equal(10, experience);
        Assert.Empty(
            await verifyContext
                .CrimeWitnesses.Where(crimeWitness => crimeWitness.CreatureId == witness.Id)
                .ToArrayAsync(TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Handle_ReturnsNotTheft_WhenContainerHasNoOwner()
    {
        var container = Builders.MakeContainer(WorldId, _theftLocationId);
        var item = await SeedItem(container.Id, OwnerType.Container);
        _context.Props.Add(container);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await AttemptTheft(
            new ItemOwnerReference(container.Id, OwnerType.Container),
            item
        );

        Assert.Equal(TheftAttemptOutcome.NotTheft, result.Outcome);
    }

    [Fact]
    public async Task Handle_ReturnsNotTheft_WhenWorkstationHasNoOwner()
    {
        var workstation = Builders.MakeWorkstation(WorldId, _theftLocationId);
        var item = await SeedItem(workstation.Id, OwnerType.Workstation);
        _context.Props.Add(workstation);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await AttemptTheft(
            new ItemOwnerReference(workstation.Id, OwnerType.Workstation),
            item
        );

        Assert.Equal(TheftAttemptOutcome.NotTheft, result.Outcome);
    }

    [Fact]
    public async Task Handle_Throws_WhenOwnerTypeDoesNotMatchThePropType()
    {
        var container = Builders.MakeContainer(WorldId, _theftLocationId);
        var item = await SeedItem(container.Id, OwnerType.Container);
        _context.Props.Add(container);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            AttemptTheft(new ItemOwnerReference(container.Id, OwnerType.Workstation), item)
        );

        Assert.Equal(
            "Owner type Workstation does not match prop type Container.",
            exception.Message
        );
    }

    [Fact]
    public async Task Handle_Throws_WhenContainerOwnerDoesNotExist()
    {
        var container = Builders.MakeContainer(WorldId, _theftLocationId);
        container.OwnerCreatureId = Guid.NewGuid();
        var item = await SeedItem(container.Id, OwnerType.Container);
        _context.Props.Add(container);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<EntityNotFoundException>(() =>
            AttemptTheft(new ItemOwnerReference(container.Id, OwnerType.Container), item)
        );
    }

    [Fact]
    public async Task Handle_CreatesEncounterWithWorkstationOccupant_WhenOwnerIsAbsent()
    {
        var owner = Builders.MakeCreature(WorldId, locationId: Guid.NewGuid());
        var worker = Builders.MakeCreature(WorldId, locationId: _theftLocationId);
        var workstation = Builders.MakeWorkstation(WorldId, _theftLocationId);
        workstation.OwnerCreatureId = owner.Id;
        workstation.OccupantId = worker.Id;
        var item = await SeedItem(workstation.Id, OwnerType.Workstation);
        _context.Creatures.AddRange(owner, worker);
        _context.Props.Add(workstation);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await AttemptTheft(
            new ItemOwnerReference(workstation.Id, OwnerType.Workstation),
            item
        );

        Assert.Equal(TheftAttemptOutcome.EncounterPending, result.Outcome);
        await using var verifyContext = db.CreateContext();
        var encounter = await verifyContext
            .Encounters.OfType<TheftEncounter>()
            .SingleAsync(
                candidate => candidate.Id == result.EncounterId,
                TestContext.Current.CancellationToken
            );
        var crime = await verifyContext
            .Crimes.OfType<TheftCrime>()
            .SingleAsync(
                candidate => candidate.Id == encounter.TheftCrimeId,
                TestContext.Current.CancellationToken
            );

        Assert.Equal(worker.Id, encounter.ConfrontingCreatureId);
        Assert.Equal(worker.Name, encounter.ConfrontingName);
        Assert.Equal(owner.Id, crime.OwnerCreatureId);
        Assert.Equal(owner.Name, crime.OwnerName);
    }

    [Fact]
    public async Task Handle_CreatesEncounterWithOwner_WhenOwnerAndWorkstationOccupantArePresent()
    {
        var owner = Builders.MakeCreature(WorldId, locationId: _theftLocationId);
        var worker = Builders.MakeCreature(WorldId, locationId: _theftLocationId);
        var workstation = Builders.MakeWorkstation(WorldId, _theftLocationId);
        workstation.OwnerCreatureId = owner.Id;
        workstation.OccupantId = worker.Id;
        var item = await SeedItem(workstation.Id, OwnerType.Workstation);
        _context.Creatures.AddRange(owner, worker);
        _context.Props.Add(workstation);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await AttemptTheft(
            new ItemOwnerReference(workstation.Id, OwnerType.Workstation),
            item
        );

        Assert.Equal(TheftAttemptOutcome.EncounterPending, result.Outcome);
        await using var verifyContext = db.CreateContext();
        var encounter = await verifyContext
            .Encounters.OfType<TheftEncounter>()
            .SingleAsync(
                candidate => candidate.Id == result.EncounterId,
                TestContext.Current.CancellationToken
            );

        Assert.Equal(owner.Id, encounter.ConfrontingCreatureId);
        Assert.Equal(owner.Name, encounter.ConfrontingName);
    }

    [Fact]
    public async Task Handle_CompletesTheftWithoutEncounter_WhenOnlyAnOrdinaryWitnessIsPresent()
    {
        var owner = Builders.MakeCreature(WorldId, locationId: Guid.NewGuid());
        var worker = Builders.MakeCreature(WorldId, locationId: Guid.NewGuid());
        var witness = Builders.MakeCreature(WorldId, locationId: _theftLocationId);
        var workstation = Builders.MakeWorkstation(WorldId, _theftLocationId);
        workstation.OwnerCreatureId = owner.Id;
        workstation.OccupantId = worker.Id;
        var item = await SeedItem(workstation.Id, OwnerType.Workstation);
        _context.Creatures.AddRange(owner, worker, witness);
        _context.Props.Add(workstation);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await AttemptTheft(
            new ItemOwnerReference(workstation.Id, OwnerType.Workstation),
            item
        );

        Assert.Equal(TheftAttemptOutcome.Completed, result.Outcome);
        await using var verifyContext = db.CreateContext();
        var crime = await verifyContext
            .Crimes.OfType<TheftCrime>()
            .SingleAsync(
                candidate => candidate.PlayerId == _player.Id,
                TestContext.Current.CancellationToken
            );

        Assert.Contains(
            witness.Id,
            await verifyContext
                .CrimeWitnesses.Where(candidate => candidate.CrimeId == crime.Id)
                .Select(candidate => candidate.CreatureId)
                .ToArrayAsync(TestContext.Current.CancellationToken)
        );
        Assert.False(
            await verifyContext
                .Encounters.OfType<TheftEncounter>()
                .AnyAsync(
                    candidate => candidate.TheftCrimeId == crime.Id,
                    TestContext.Current.CancellationToken
                )
        );
    }

    [Fact]
    public async Task Handle_LeavesPickpocketedItemWithOwnerAndRecordsWitnesses_WhenCaught()
    {
        // Arrange
        var owner = Builders.MakeCreature(WorldId, locationId: _theftLocationId);
        var bystander = Builders.MakeCreature(WorldId, locationId: _theftLocationId);
        var item = await SeedItem(owner.Id, OwnerType.Creature);
        _context.Creatures.AddRange(owner, bystander);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await AttemptTheft(new ItemOwnerReference(owner.Id, OwnerType.Creature), item);

        // Assert
        Assert.Equal(TheftAttemptOutcome.EncounterPending, result.Outcome);
        await using var verifyContext = db.CreateContext();
        var retainedItem = await verifyContext.Items.SingleAsync(
            candidate => candidate.Id == item.Id,
            TestContext.Current.CancellationToken
        );
        var encounter = await verifyContext
            .Encounters.OfType<TheftEncounter>()
            .SingleAsync(
                candidate => candidate.Id == result.EncounterId,
                TestContext.Current.CancellationToken
            );
        var crime = await verifyContext
            .Crimes.OfType<TheftCrime>()
            .SingleAsync(
                candidate => candidate.Id == encounter.TheftCrimeId,
                TestContext.Current.CancellationToken
            );
        var witnessIds = await verifyContext
            .CrimeWitnesses.Where(candidate => candidate.CrimeId == crime.Id)
            .Select(candidate => candidate.CreatureId)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(owner.Id, retainedItem.Ownership.OwnerId);
        Assert.Equal(OwnerType.Creature, retainedItem.Ownership.OwnerType);
        Assert.Equal(EncounterState.Active, encounter.State);
        Assert.Equal(owner.Id, encounter.ConfrontingCreatureId);
        Assert.Equal(owner.Id, encounter.SourceOwnerId);
        Assert.Equal(OwnerType.Creature, encounter.SourceOwnerType);
        Assert.Equal(CrimeResolution.Pending, crime.Resolution);
        Assert.Null(crime.Outcome);
        Assert.Contains(owner.Id, witnessIds);
        Assert.Contains(bystander.Id, witnessIds);
        Assert.Contains(
            _serviceProvider.GetRequiredService<TestGameClientEventSink>().EnqueuedEvents,
            gameEvent => gameEvent == new CrimeWitnessedEvent(CrimeKind.Theft)
        );
    }

    [Fact]
    public async Task Handle_SetsWitnessesToAlerted_WhenCaught()
    {
        // Arrange
        var owner = Builders.MakeCreature(WorldId, locationId: _theftLocationId);
        var bystander = Builders.MakeCreature(WorldId, locationId: _theftLocationId);
        var item = await SeedItem(owner.Id, OwnerType.Creature);
        _context.Creatures.AddRange(owner, bystander);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await AttemptTheft(new ItemOwnerReference(owner.Id, OwnerType.Creature), item);

        // Assert
        await using var verifyContext = db.CreateContext();
        var updatedBystander = await verifyContext.Creatures.SingleAsync(
            creature => creature.Id == bystander.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(CreatureState.Alerted, updatedBystander.State);
    }

    [Fact]
    public async Task Handle_ExcludesSleepingCreatures_FromTheftWitnesses()
    {
        // Arrange
        var owner = Builders.MakeCreature(WorldId, locationId: _theftLocationId);
        var sleepingBystander = Builders.MakeCreature(
            WorldId,
            locationId: _theftLocationId,
            state: CreatureState.Sleeping
        );
        var item = await SeedItem(owner.Id, OwnerType.Creature);
        _context.Creatures.AddRange(owner, sleepingBystander);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await AttemptTheft(new ItemOwnerReference(owner.Id, OwnerType.Creature), item);

        // Assert
        await using var verifyContext = db.CreateContext();
        var encounter = await verifyContext
            .Encounters.OfType<TheftEncounter>()
            .SingleAsync(
                candidate => candidate.Id == result.EncounterId,
                TestContext.Current.CancellationToken
            );
        var witnessIds = await verifyContext
            .CrimeWitnesses.Where(candidate => candidate.CrimeId == encounter.TheftCrimeId)
            .Select(candidate => candidate.CreatureId)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(sleepingBystander.Id, witnessIds);
    }

    [Fact]
    public async Task Handle_ExcludesNonHumanoidCreatures_FromTheftWitnesses()
    {
        // Arrange
        var owner = Builders.MakeCreature(WorldId, locationId: _theftLocationId);
        var beastBystander = Builders.MakeCreature(
            WorldId,
            locationId: _theftLocationId,
            creatureType: CreatureType.Beast
        );
        var item = await SeedItem(owner.Id, OwnerType.Creature);
        _context.Creatures.AddRange(owner, beastBystander);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await AttemptTheft(new ItemOwnerReference(owner.Id, OwnerType.Creature), item);

        // Assert
        await using var verifyContext = db.CreateContext();
        var encounter = await verifyContext
            .Encounters.OfType<TheftEncounter>()
            .SingleAsync(
                candidate => candidate.Id == result.EncounterId,
                TestContext.Current.CancellationToken
            );
        var witnessIds = await verifyContext
            .CrimeWitnesses.Where(candidate => candidate.CrimeId == encounter.TheftCrimeId)
            .Select(candidate => candidate.CreatureId)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(beastBystander.Id, witnessIds);
    }

    [Fact]
    public async Task Handle_MovesContainerItemAndCreatesEncounter_WhenOwnerCatchesThePlayer()
    {
        // Arrange
        var owner = Builders.MakeCreature(WorldId, locationId: _theftLocationId);
        var container = Builders.MakeContainer(WorldId, _theftLocationId);
        container.OwnerCreatureId = owner.Id;
        var item = await SeedItem(container.Id, OwnerType.Container);
        _context.Creatures.Add(owner);
        _context.Props.Add(container);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await AttemptTheft(
            new ItemOwnerReference(container.Id, OwnerType.Container),
            item
        );

        // Assert
        Assert.Equal(TheftAttemptOutcome.EncounterPending, result.Outcome);
        await using var verifyContext = db.CreateContext();
        var movedItem = await verifyContext.Items.SingleAsync(
            candidate => candidate.Id == item.Id,
            TestContext.Current.CancellationToken
        );
        var encounter = await verifyContext
            .Encounters.OfType<TheftEncounter>()
            .SingleAsync(
                candidate => candidate.Id == result.EncounterId,
                TestContext.Current.CancellationToken
            );
        var crime = await verifyContext
            .Crimes.OfType<TheftCrime>()
            .SingleAsync(
                candidate => candidate.Id == encounter.TheftCrimeId,
                TestContext.Current.CancellationToken
            );
        var witness = await verifyContext.CrimeWitnesses.SingleAsync(
            candidate => candidate.CrimeId == crime.Id && candidate.CreatureId == owner.Id,
            TestContext.Current.CancellationToken
        );

        Assert.Equal(_player.Id, movedItem.Ownership.OwnerId);
        Assert.Equal(OwnerType.Creature, movedItem.Ownership.OwnerType);
        Assert.Equal(container.Id, encounter.SourceOwnerId);
        Assert.Equal(OwnerType.Container, encounter.SourceOwnerType);
        Assert.Equal(TheftCrimeOutcome.Taken, crime.Outcome);
        Assert.Equal(CrimeWitnessResolution.Pending, witness.Resolution);
    }

    private async Task<TheftAttemptResult> AttemptTheft(ItemOwnerReference from, Item item) =>
        await _handler.Handle(
            new AttemptTheftCommand
            {
                From = from,
                Items = [new ItemSelection(item.Id, item.Quantity)],
                PlayerId = _player.Id,
                WorldId = WorldId,
            },
            TestContext.Current.CancellationToken
        );

    private async Task<Weapon> SeedItem(Guid ownerId, OwnerType ownerType)
    {
        var item = Builders.MakeWeapon(WorldId);
        item.Quantity = 1;
        item.Ownership.OwnerId = ownerId;
        item.Ownership.OwnerType = ownerType;
        _context.Items.Add(item);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return item;
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
        where T : class
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class TestChanceRoller : IChanceRoller
    {
        public bool Result { get; set; } = true;

        public bool Roll(float chance) => Result;
    }
}
