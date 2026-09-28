using Microsoft.EntityFrameworkCore;
using TRPG.Application.Creatures.Commands;
using TRPG.Data;
using TRPG.Domain;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Creatures.Commands;

public sealed class UpdateCreaturesCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private TrpgDbContext _context = null!;
    private UpdateCreaturesCommandHandler _handler = null!;
    private readonly Creature _creature = Builders.MakeCreature(locationId: Guid.NewGuid());

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _handler = new UpdateCreaturesCommandHandler(_context);

        _context.Creatures.Add(_creature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_UpdatesLocation_WhenSet()
    {
        // Arrange
        var locationId = Guid.NewGuid();

        // Act
        await _handler.Handle(
            new UpdateCreaturesCommand { CreatureIds = [_creature.Id], LocationId = locationId },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await ReadCreature(_creature.Id);
        Assert.Equal(locationId, updated.LocationId);
    }

    [Fact]
    public async Task Handle_RecordsThePriorLocationAsPreviousLocation_WhenLocationIsSet()
    {
        // Arrange
        var originalLocationId = _creature.LocationId;

        // Act
        await _handler.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [_creature.Id],
                LocationId = Guid.NewGuid(),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await ReadCreature(_creature.Id);
        Assert.Equal(originalLocationId, updated.PreviousLocationId);
    }

    [Fact]
    public async Task Handle_KeepsThePreviousLocation_WhenTheLocationIsAlreadyTheRequestedOne()
    {
        // Arrange
        var previousLocationId = Guid.NewGuid();
        await _context
            .Creatures.Where(creature => creature.Id == _creature.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(c => c.PreviousLocationId, previousLocationId),
                TestContext.Current.CancellationToken
            );

        // Act
        await _handler.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [_creature.Id],
                LocationId = _creature.LocationId,
                Name = "Renamed",
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await ReadCreature(_creature.Id);
        Assert.Equal(previousLocationId, updated.PreviousLocationId);
        Assert.Equal("Renamed", updated.Name);
    }

    [Fact]
    public async Task Handle_DoesNotWriteTheRow_WhenItAlreadyHoldsTheRequestedValues()
    {
        // Arrange
        var command = new UpdateCreaturesCommand
        {
            CreatureIds = [_creature.Id],
            LocationId = _creature.LocationId,
            Name = "Renamed",
        };
        await _handler.Handle(command, TestContext.Current.CancellationToken);
        var versionBefore = await ReadRowVersion();

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(versionBefore, await ReadRowVersion());
    }

    [Fact]
    public async Task Handle_WritesTheRow_WhenAnyRequestedValueDiffers()
    {
        // Arrange
        await _handler.Handle(
            new UpdateCreaturesCommand { CreatureIds = [_creature.Id], Name = "First" },
            TestContext.Current.CancellationToken
        );
        var versionBefore = await ReadRowVersion();

        // Act
        await _handler.Handle(
            new UpdateCreaturesCommand { CreatureIds = [_creature.Id], Name = "Second" },
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.NotEqual(versionBefore, await ReadRowVersion());
    }

    [Fact]
    public async Task Handle_LeavesLocationUnchanged_WhenNotSet()
    {
        // Act
        await _handler.Handle(
            new UpdateCreaturesCommand { CreatureIds = [_creature.Id], Name = "Renamed" },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await ReadCreature(_creature.Id);
        Assert.Equal(_creature.LocationId, updated.LocationId);
    }

    [Fact]
    public async Task Handle_UpdatesLastRegenGameTime_WhenSet()
    {
        // Act
        await _handler.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [_creature.Id],
                LastRegenGameTime = GameClock.Epoch + TimeSpan.FromHours(3),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await ReadCreature(_creature.Id);
        Assert.Equal(GameClock.Epoch + TimeSpan.FromHours(3), updated.LastRegenGameTime);
    }

    [Fact]
    public async Task Handle_LeavesLastRegenGameTimeUnchanged_WhenNotSet()
    {
        // Arrange
        var originalLastRegenGameTime = _creature.LastRegenGameTime;

        // Act
        await _handler.Handle(
            new UpdateCreaturesCommand { CreatureIds = [_creature.Id], Name = "Renamed" },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await ReadCreature(_creature.Id);
        Assert.Equal(originalLastRegenGameTime, updated.LastRegenGameTime);
    }

    [Fact]
    public async Task Handle_DoesNothing_WhenNoFieldsAreSet()
    {
        // Act
        await _handler.Handle(
            new UpdateCreaturesCommand { CreatureIds = [_creature.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await ReadCreature(_creature.Id);
        Assert.Equal(_creature.LocationId, updated.LocationId);
        Assert.Equal(_creature.LastRegenGameTime, updated.LastRegenGameTime);
        Assert.Equal(_creature.Name, updated.Name);
        Assert.Equal(_creature.IsRestrained, updated.IsRestrained);
    }

    [Fact]
    public async Task Handle_UpdatesName_WhenSet()
    {
        // Act
        await _handler.Handle(
            new UpdateCreaturesCommand { CreatureIds = [_creature.Id], Name = "Grukk the Butcher" },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await ReadCreature(_creature.Id);
        Assert.Equal("Grukk the Butcher", updated.Name);
    }

    [Fact]
    public async Task Handle_LeavesNameUnchanged_WhenNotSet()
    {
        // Act
        await _handler.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [_creature.Id],
                LastRegenGameTime = GameClock.Epoch + TimeSpan.FromHours(1),
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await ReadCreature(_creature.Id);
        Assert.Equal(_creature.Name, updated.Name);
    }

    [Fact]
    public async Task Handle_UpdatesIsRestrained_WhenSet()
    {
        // Act
        await _handler.Handle(
            new UpdateCreaturesCommand { CreatureIds = [_creature.Id], IsRestrained = true },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updated = await ReadCreature(_creature.Id);
        Assert.True(updated.IsRestrained);
    }

    [Fact]
    public async Task Handle_UpdatesLocation_ForEveryCreatureId_WhenGivenMultiple()
    {
        // Arrange
        var otherCreature = Builders.MakeCreature();
        _context.Creatures.Add(otherCreature);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var locationId = Guid.NewGuid();

        // Act
        await _handler.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [_creature.Id, otherCreature.Id],
                LocationId = locationId,
            },
            TestContext.Current.CancellationToken
        );

        // Assert
        var updatedCreature = await ReadCreature(_creature.Id);
        var updatedOther = await ReadCreature(otherCreature.Id);
        Assert.Equal(locationId, updatedCreature.LocationId);
        Assert.Equal(locationId, updatedOther.LocationId);
    }

    private async Task<Creature> ReadCreature(Guid creatureId)
    {
        await using var verifyContext = db.CreateContext();
        return await verifyContext
            .Creatures.AsNoTracking()
            .SingleAsync(
                creature => creature.Id == creatureId,
                TestContext.Current.CancellationToken
            );
    }

    private async Task<long> ReadRowVersion()
    {
        await using var verifyContext = db.CreateContext();
        return await verifyContext
            .Database.SqlQuery<long>(
                $"SELECT xmin::text::bigint AS \"Value\" FROM creatures WHERE id = {_creature.Id}"
            )
            .SingleAsync(TestContext.Current.CancellationToken);
    }
}
