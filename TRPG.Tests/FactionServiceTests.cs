using TRPG.Data;
using TRPG.Models;
using TRPG.Services;
using TRPG.Tests.Helpers;

namespace TRPG.Tests;

[Collection("Database")]
public class FactionServiceTests(DatabaseFixture db) : IAsyncLifetime
{
    private TrpgDbContext _context = null!;
    private FactionService _service = null!;
    private Faction _faction = null!;
    private readonly Guid _personId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        _context = db.CreateContext();
        _service = new FactionService(_context);

        _faction = Builders.MakeFaction();
        _context.Factions.Add(_faction);
        await _context.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _context.DisposeAsync();

    [Fact]
    public async Task GetById_ReturnsNull_WhenNotFound()
    {
        // Act
        var result = await _service.GetById(Guid.NewGuid());

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetById_ReturnsFaction_WhenExists()
    {
        // Act
        var result = await _service.GetById(_faction.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_faction.Id, result.Id);
    }

    [Fact]
    public async Task AddMember_AddsMemberToFaction()
    {
        // Act
        await _service.AddMember(_faction.Id, _personId, FactionRole.Member);

        // Assert
        var members = await _service.GetAllMembersByFactionId(_faction.Id);
        Assert.Single(members);
        Assert.Equal(_personId, members[0].PersonId);
        Assert.Equal(FactionRole.Member, members[0].Role);
    }

    [Fact]
    public async Task GetAllMembershipsByPersonId_ReturnsMemberships()
    {
        // Arrange
        var faction2 = Builders.MakeFaction();
        _context.Factions.Add(faction2);
        await _context.SaveChangesAsync();

        await _service.AddMember(_faction.Id, _personId, FactionRole.Member);
        await _service.AddMember(faction2.Id, _personId, FactionRole.Leader);

        // Act
        var result = await _service.GetAllMembershipsByPersonId(_personId);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, m => m.FactionId == _faction.Id);
        Assert.Contains(result, m => m.FactionId == faction2.Id);
    }

    [Fact]
    public async Task GetAllMembersByFactionId_ReturnsMembers()
    {
        // Arrange
        var personId2 = Guid.NewGuid();
        await _service.AddMember(_faction.Id, _personId, FactionRole.Leader);
        await _service.AddMember(_faction.Id, personId2, FactionRole.Member);

        // Act
        var result = await _service.GetAllMembersByFactionId(_faction.Id);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, m => m.PersonId == _personId);
        Assert.Contains(result, m => m.PersonId == personId2);
    }

    [Fact]
    public async Task UpdateMemberRole_UpdatesRole()
    {
        // Arrange
        await _service.AddMember(_faction.Id, _personId, FactionRole.Member);

        // Act
        await _service.UpdateMemberRole(_faction.Id, _personId, FactionRole.Leader);

        // Assert
        var members = await _service.GetAllMembersByFactionId(_faction.Id);
        Assert.Equal(FactionRole.Leader, members[0].Role);
    }

    [Fact]
    public async Task UpdateMemberRole_Throws_WhenNotMember()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.UpdateMemberRole(_faction.Id, Guid.NewGuid(), FactionRole.Leader));
    }

    [Fact]
    public async Task RemoveMember_RemovesMembership()
    {
        // Arrange
        await _service.AddMember(_faction.Id, _personId, FactionRole.Member);

        // Act
        await _service.RemoveMember(_faction.Id, _personId);

        // Assert
        var members = await _service.GetAllMembersByFactionId(_faction.Id);
        Assert.Empty(members);
    }
}
