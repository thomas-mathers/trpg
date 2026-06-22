using TRPG.Data;
using TRPG.Services;

namespace TRPG.Tests;

[Collection("Database")]
public sealed class ReputationServiceTests(DatabaseFixture db) : IAsyncLifetime {
    private readonly Guid _factionId = Guid.NewGuid();
    private readonly Guid _personId = Guid.NewGuid();
    private TrpgDbContext _context = null!;
    private ReputationService _service = null!;

    public ValueTask InitializeAsync() {
        _context = db.CreateContext();
        _service = new ReputationService(_context);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task AdjustReputation_CreatesReputation_WhenFirstCall() {
        // Act
        await _service.AdjustReputation(_personId, _factionId, 10, TestContext.Current.CancellationToken);

        // Assert
        var reputations = await _service.GetAllByPersonId(_personId, TestContext.Current.CancellationToken);
        Assert.Single(reputations);
        Assert.Equal(10, reputations[0].Score);
    }

    [Fact]
    public async Task AdjustReputation_IncrementsScore_WhenSubsequentCall() {
        // Arrange
        var personId = Guid.NewGuid();
        var factionId = Guid.NewGuid();
        await _service.AdjustReputation(personId, factionId, 10, TestContext.Current.CancellationToken);

        // Act
        await _service.AdjustReputation(personId, factionId, 5, TestContext.Current.CancellationToken);

        // Assert
        var reputations = await _service.GetAllByPersonId(personId, TestContext.Current.CancellationToken);
        Assert.Single(reputations);
        Assert.Equal(15, reputations[0].Score);
    }

    [Fact]
    public async Task AdjustReputation_SupportsNegativeDelta() {
        // Arrange
        var personId = Guid.NewGuid();
        var factionId = Guid.NewGuid();
        await _service.AdjustReputation(personId, factionId, 20, TestContext.Current.CancellationToken);

        // Act
        await _service.AdjustReputation(personId, factionId, -8, TestContext.Current.CancellationToken);

        // Assert
        var reputations = await _service.GetAllByPersonId(personId, TestContext.Current.CancellationToken);
        Assert.Equal(12, reputations[0].Score);
    }

    [Fact]
    public async Task GetAllByPersonId_ReturnsReputationsForPerson() {
        // Arrange
        var personId = Guid.NewGuid();
        var factionId1 = Guid.NewGuid();
        var factionId2 = Guid.NewGuid();
        await _service.AdjustReputation(personId, factionId1, 5, TestContext.Current.CancellationToken);
        await _service.AdjustReputation(personId, factionId2, 10, TestContext.Current.CancellationToken);

        // Act
        var result = await _service.GetAllByPersonId(personId, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.FactionId == factionId1 && r.Score == 5);
        Assert.Contains(result, r => r.FactionId == factionId2 && r.Score == 10);
    }
}