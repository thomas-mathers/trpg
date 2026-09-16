using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.LocationSimulation.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.LocationSimulation.Commands;

public sealed class SeedCourierQuestCommandTests : IAsyncLifetime, IClassFixture<DatabaseFixture>
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly Guid _stateId = Guid.NewGuid();
    private readonly Guid _cityId = Guid.NewGuid();
    private readonly DatabaseFixture _database;
    private readonly Location _entranceLocation;
    private readonly Location _giverLocation;
    private readonly Creature _giver;
    private readonly Location _recipientLocation;
    private readonly Creature _recipient;
    private TrpgDbContext _context = null!;
    private ServiceProvider _services = null!;
    private ICommandHandler<SeedCourierQuestCommand, bool> _handler = null!;

    public SeedCourierQuestCommandTests(DatabaseFixture database)
    {
        _database = database;
        _entranceLocation = Builders.MakeLocation(_worldId, _stateId, cityId: _cityId);
        // The giver works elsewhere in the city, not at the seed/entrance location itself, and
        // is a profession the courier-quest giver filter allows.
        _giverLocation = Builders.MakeLocation(_worldId, _stateId, cityId: _cityId);
        _giver = Builders.MakeCreature(
            _worldId,
            profession: Profession.Merchant,
            locationId: _giverLocation.Id,
            name: "Giver"
        );
        _recipientLocation = Builders.MakeLocation(_worldId, _stateId, cityId: _cityId);
        _recipient = Builders.MakeCreature(
            _worldId,
            locationId: _recipientLocation.Id,
            name: "Recipient"
        );
    }

    public async ValueTask InitializeAsync()
    {
        _context = _database.CreateContext();
        _services = new ServiceCollection().AddTrpgTestServices(_context).BuildServiceProvider();
        _handler = _services.GetRequiredService<ICommandHandler<SeedCourierQuestCommand, bool>>();

        _context.Locations.AddRange(_entranceLocation, _giverLocation, _recipientLocation);
        _context.Creatures.AddRange(_giver, _recipient);
        _context.CreatureJobs.Add(
            Builders.MakeCreatureJob(_giver.Id, locationId: _giverLocation.Id, worldId: _worldId)
        );
        _context.CreatureJobs.Add(
            Builders.MakeCreatureJob(
                _recipient.Id,
                locationId: _recipientLocation.Id,
                worldId: _worldId
            )
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_OffersADeliveryQuest_WhenAGiverAndRecipientAreBothInTheCity()
    {
        // Act
        var result = await _handler.Handle(
            new SeedCourierQuestCommand
            {
                WorldId = _worldId,
                PlayerId = Guid.NewGuid(),
                LocationId = _entranceLocation.Id,
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
            .QuestObjectives.OfType<DeliverItemObjective>()
            .SingleAsync(o => o.QuestId == quest.Id, TestContext.Current.CancellationToken);
        Assert.Equal(_recipient.Id, objective.RecipientId);
        var package = await _context.Items.SingleAsync(
            i => i.Id == objective.ItemId,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(_giver.Id, package.Ownership.OwnerId);
        Assert.Equal(OwnerType.Creature, package.Ownership.OwnerType);
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenTheSeedLocationHasNoCity()
    {
        // Act
        var result = await _handler.Handle(
            new SeedCourierQuestCommand
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
    public async Task Handle_ReturnsFalse_WhenTheOnlyCandidateHasADisqualifyingProfession()
    {
        // Arrange — a knight isn't a plausible courier-quest giver
        await _context
            .Creatures.Where(creature => creature.Id == _giver.Id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(c => c.Profession, Profession.Knight),
                TestContext.Current.CancellationToken
            );

        // Act
        var result = await _handler.Handle(
            new SeedCourierQuestCommand
            {
                WorldId = _worldId,
                PlayerId = Guid.NewGuid(),
                LocationId = _entranceLocation.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenNoOtherResidentExistsInTheCity()
    {
        // Arrange — remove the only other job-holder in the city
        _context.CreatureJobs.RemoveRange(
            _context.CreatureJobs.Where(job => job.CreatureId == _recipient.Id)
        );
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new SeedCourierQuestCommand
            {
                WorldId = _worldId,
                PlayerId = Guid.NewGuid(),
                LocationId = _entranceLocation.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenTheOnlyRecipientAlreadyHasAnActiveDelivery()
    {
        // Arrange
        var playerId = Guid.NewGuid();
        var existingQuest = Builders.MakeQuest(_giver.Id, _worldId);
        var existingObjective = new DeliverItemObjective
        {
            WorldId = _worldId,
            QuestId = existingQuest.Id,
            ItemId = Guid.NewGuid(),
            RecipientId = _recipient.Id,
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
            new SeedCourierQuestCommand
            {
                WorldId = _worldId,
                PlayerId = playerId,
                LocationId = _entranceLocation.Id,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result);
    }
}
