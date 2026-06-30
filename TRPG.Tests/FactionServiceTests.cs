using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class FactionServiceTests(DatabaseFixture db) : IAsyncLifetime {
    private readonly Guid _personId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private Faction _faction = null!;
    private FactionService _service = null!;

    public async ValueTask InitializeAsync() {
        _context = db.CreateContext();
        _service = new FactionService(_context);

        _faction = Builders.MakeFaction();
        _context.Factions.Add(_faction);
        await _context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync() {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task GetById_ReturnsNull_WhenNotFound() {
        // Act
        var result = await _service.GetById(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetById_ReturnsFaction_WhenExists() {
        // Act
        var result = await _service.GetById(_faction.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_faction.Id, result.Id);
    }

    [Fact]
    public async Task AddMember_AddsMemberToFaction() {
        // Act
        await _service.AddMember(_faction.Id, _personId, FactionRole.Member, TestContext.Current.CancellationToken);

        // Assert
        var members = await _service.GetAllMembersByFactionId(_faction.Id, TestContext.Current.CancellationToken);
        Assert.Single(members);
        Assert.Equal(_personId, members.First().PersonId);
        Assert.Equal(FactionRole.Member, members.First().Role);
    }

    [Fact]
    public async Task GetAllMembershipsByPersonId_ReturnsMemberships() {
        // Arrange
        var faction2 = Builders.MakeFaction();
        _context.Factions.Add(faction2);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _service.AddMember(_faction.Id, _personId, FactionRole.Member, TestContext.Current.CancellationToken);
        await _service.AddMember(faction2.Id, _personId, FactionRole.Leader, TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetAllMembershipsByPersonId(_personId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, m => m.FactionId == _faction.Id);
        Assert.Contains(result, m => m.FactionId == faction2.Id);
    }

    [Fact]
    public async Task GetAllMembersByFactionId_ReturnsMembers() {
        // Arrange
        var personId2 = Guid.NewGuid();
        await _service.AddMember(_faction.Id, _personId, FactionRole.Leader, TestContext.Current.CancellationToken);
        await _service.AddMember(_faction.Id, personId2, FactionRole.Member, TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetAllMembersByFactionId(_faction.Id, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, m => m.PersonId == _personId);
        Assert.Contains(result, m => m.PersonId == personId2);
    }

    [Fact]
    public async Task UpdateMemberRole_UpdatesRole() {
        // Arrange
        await _service.AddMember(_faction.Id, _personId, FactionRole.Member, TestContext.Current.CancellationToken);

        // Act
        await _service.UpdateMemberRole(_faction.Id, _personId, FactionRole.Leader,
            TestContext.Current.CancellationToken);

        // Assert
        var members = await _service.GetAllMembersByFactionId(_faction.Id, TestContext.Current.CancellationToken);
        Assert.Equal(FactionRole.Leader, members.First().Role);
    }

    [Fact]
    public async Task UpdateMemberRole_Throws_WhenNotMember() {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.UpdateMemberRole(_faction.Id, Guid.NewGuid(), FactionRole.Leader,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RemoveMember_RemovesMembership() {
        // Arrange
        await _service.AddMember(_faction.Id, _personId, FactionRole.Member, TestContext.Current.CancellationToken);

        // Act
        await _service.RemoveMember(_faction.Id, _personId, TestContext.Current.CancellationToken);

        // Assert
        var members = await _service.GetAllMembersByFactionId(_faction.Id, TestContext.Current.CancellationToken);
        Assert.Empty(members);
    }
}