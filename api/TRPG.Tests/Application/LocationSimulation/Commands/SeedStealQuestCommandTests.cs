using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.LocationSimulation.Commands;

public sealed class SeedStealQuestCommandTests : IAsyncLifetime, IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _stateId = Guid.NewGuid();
    private readonly DatabaseFixture _database;
    private readonly City _city;
    private readonly Location _giverLocation;
    private readonly Creature _giver;
    private readonly Location _pickpocketLocation;
    private readonly Creature _pickpocketTarget;
    private readonly Location _shopLocation;
    private readonly Building _shop;
    private readonly Workstation _workstation;
    private readonly Location _houseLocation;
    private readonly Building _house;
    private readonly Container _container;
    private TrpgDbContext _context = null!;
    private ServiceProvider _services = null!;
    private ICommandHandler<SeedStealQuestCommand, bool> _handler = null!;

    public SeedStealQuestCommandTests(DatabaseFixture database)
    {
        _database = database;
        _city = Builders.MakeCity(_stateId, Guid.NewGuid(), worldId: _worldId);
        _giverLocation = Builders.MakeLocation(_worldId, _stateId, cityId: _city.Id);
        _giver = Builders.MakeCreature(_worldId, locationId: _giverLocation.Id, name: "Giver");

        _pickpocketLocation = Builders.MakeLocation(_worldId, _stateId, cityId: _city.Id);
        _pickpocketTarget = Builders.MakeCreature(
            _worldId,
            locationId: _pickpocketLocation.Id,
            name: "Mark"
        );

        _shopLocation = Builders.MakeLocation(_worldId, _stateId, cityId: _city.Id);
        _shop = Builders.MakeBuilding(
            exteriorLocationId: Guid.NewGuid(),
            worldId: _worldId,
            name: "The Silver Setting"
        );
        _workstation = Builders.MakeWorkstation(_worldId, locationId: _shopLocation.Id);

        _houseLocation = Builders.MakeLocation(_worldId, _stateId, cityId: _city.Id);
        _house = Builders.MakeBuilding(
            exteriorLocationId: Guid.NewGuid(),
            worldId: _worldId,
            name: "Riverside Cottage"
        );
        _container = Builders.MakeContainer(_worldId, _houseLocation.Id);
    }

    public async ValueTask InitializeAsync()
    {
        _context = _database.CreateContext();
        _services = new ServiceCollection().AddTrpgTestServices(_context).BuildServiceProvider();
        _handler = _services.GetRequiredService<ICommandHandler<SeedStealQuestCommand, bool>>();

        _context.Cities.Add(_city);
        _context.Locations.AddRange(
            _giverLocation,
            _pickpocketLocation,
            _shopLocation,
            _houseLocation
        );
        _context.Creatures.AddRange(_giver, _pickpocketTarget);
        _context.CreatureJobs.AddRange(
            Builders.MakeCreatureJob(_giver.Id, locationId: _giverLocation.Id, worldId: _worldId),
            Builders.MakeCreatureJob(
                _pickpocketTarget.Id,
                locationId: _pickpocketLocation.Id,
                worldId: _worldId
            )
        );
        _context.Buildings.AddRange(_shop, _house);
        _context.Rooms.AddRange(
            Builders.MakeRoom(_shop.Id, worldId: _worldId, locationId: _shopLocation.Id),
            Builders.MakeRoom(_house.Id, worldId: _worldId, locationId: _houseLocation.Id)
        );
        _context.Props.AddRange(_workstation, _container);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_OffersAStealQuest_WhenEligibleTargetsExistInTheCity()
    {
        // Act
        var result = await _handler.Handle(
            new SeedStealQuestCommand
            {
                WorldId = _worldId,
                PlayerId = Guid.NewGuid(),
                LocationId = _giverLocation.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(result);
        var quest = await _context.Quests.SingleAsync(
            q => q.WorldId == _worldId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_giver.Id, quest.GiverId);
        var objective = await _context
            .QuestObjectives.OfType<GiveItemsObjective>()
            .SingleAsync(o => o.QuestId == quest.Id, TestContext.Current.CancellationToken);
        Assert.Equal(_giver.Id, objective.RecipientId);
        Assert.Equal(3, objective.ItemIds.Count);
        Assert.Equal(3, objective.RequiredAmount);

        var items = await _context
            .Items.Where(item => objective.ItemIds.Contains(item.Id))
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, items.Length);
        Assert.Contains(items, item => item.Name.StartsWith("Mark's ", StringComparison.Ordinal));
        Assert.Contains(
            items,
            item => item.Name.StartsWith("The Silver Setting's ", StringComparison.Ordinal)
        );
        Assert.Contains(
            items,
            item => item.Name.StartsWith("Riverside Cottage's ", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenNoGiverCandidateIsAtTheLocation()
    {
        // Act
        var result = await _handler.Handle(
            new SeedStealQuestCommand
            {
                WorldId = _worldId,
                PlayerId = Guid.NewGuid(),
                LocationId = Guid.NewGuid(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenNotEnoughTargetsExistInTheCity()
    {
        // Arrange — an isolated city with only two eligible targets, one short of ItemCount.
        // Deliberately does not touch the shared fixture's three targets: tests in this class share
        // one database with no rollback between them, so mutating shared state here would corrupt
        // whichever test happens to run afterward.
        var stateId = Guid.NewGuid();
        var city = Builders.MakeCity(stateId, Guid.NewGuid(), worldId: _worldId);
        var giverLocation = Builders.MakeLocation(_worldId, stateId, cityId: city.Id);
        var giver = Builders.MakeCreature(_worldId, locationId: giverLocation.Id, name: "Giver2");
        var pickpocketLocation = Builders.MakeLocation(_worldId, stateId, cityId: city.Id);
        var pickpocketTarget = Builders.MakeCreature(
            _worldId,
            locationId: pickpocketLocation.Id,
            name: "Mark2"
        );
        var shopLocation = Builders.MakeLocation(_worldId, stateId, cityId: city.Id);
        var workstation = Builders.MakeWorkstation(_worldId, locationId: shopLocation.Id);

        _context.Cities.Add(city);
        _context.Locations.AddRange(giverLocation, pickpocketLocation, shopLocation);
        _context.Creatures.AddRange(giver, pickpocketTarget);
        _context.CreatureJobs.AddRange(
            Builders.MakeCreatureJob(giver.Id, locationId: giverLocation.Id, worldId: _worldId),
            Builders.MakeCreatureJob(
                pickpocketTarget.Id,
                locationId: pickpocketLocation.Id,
                worldId: _worldId
            )
        );
        _context.Props.Add(workstation);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new SeedStealQuestCommand
            {
                WorldId = _worldId,
                PlayerId = Guid.NewGuid(),
                LocationId = giverLocation.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task Handle_ExcludesTargetsAtTheGiversOwnLocation()
    {
        // Arrange — a fourth, otherwise-eligible target sitting at the giver's own location
        var atGiverLocation = Builders.MakeCreature(
            _worldId,
            locationId: _giverLocation.Id,
            name: "Bystander"
        );
        _context.Creatures.Add(atGiverLocation);
        _context.CreatureJobs.Add(
            Builders.MakeCreatureJob(
                atGiverLocation.Id,
                locationId: _giverLocation.Id,
                worldId: _worldId
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        await _handler.Handle(
            new SeedStealQuestCommand
            {
                WorldId = _worldId,
                PlayerId = Guid.NewGuid(),
                LocationId = _giverLocation.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert — no item is ever spawned owned by the bystander, regardless of which other
        // GiveItemsObjective rows other tests in this class have already created for the shared
        // giver (tests share one database with no rollback between them).
        var hasItemForBystander = await _context.Items.AnyAsync(
            item => item.Ownership.OwnerId == atGiverLocation.Id,
            TestContext.Current.CancellationToken
        );
        Assert.False(hasItemForBystander);
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenThePlayerAlreadyHasAnActiveStealQuestFromTheGiver()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var existingQuest = Builders.MakeQuest(_giver.Id, _worldId);
        var existingObjective = new GiveItemsObjective
        {
            WorldId = _worldId,
            QuestId = existingQuest.Id,
            ItemIds = [Guid.NewGuid()],
            RecipientId = _giver.Id,
        };
        _context.Quests.Add(existingQuest);
        _context.QuestObjectives.Add(existingObjective);
        _context.CreatureQuests.Add(
            new CreatureQuest
            {
                CreatureId = playerId,
                QuestId = existingQuest.Id,
                Status = QuestStatus.Accepted,
                WorldId = _worldId,
            }
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new SeedStealQuestCommand
            {
                WorldId = _worldId,
                PlayerId = playerId,
                LocationId = _giverLocation.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result);
    }
}
