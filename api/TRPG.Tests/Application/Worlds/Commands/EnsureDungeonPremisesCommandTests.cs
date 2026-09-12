using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TRPG.Application.Common.Commands;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Application.Worlds.Commands;
using TRPG.Data;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Commands;

public sealed class EnsureDungeonPremisesCommandTests(DatabaseFixture db)
    : IAsyncLifetime,
        IClassFixture<DatabaseFixture>
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly FakeChatClient _chatClient = new() { ChatResponseText = "A mine abandoned." };
    private TrpgDbContext _context = null!;
    private ServiceProvider _serviceProvider = null!;
    private ICommandHandler<EnsureDungeonPremisesCommand> _handler = null!;

    public async ValueTask InitializeAsync()
    {
        _context = db.CreateContext();
        _serviceProvider = new ServiceCollection()
            .AddTrpgTestServices(_context)
            .AddSingleton(new DungeonPremiseGenerator(_chatClient))
            .BuildServiceProvider();
        _handler = _serviceProvider.GetRequiredService<
            ICommandHandler<EnsureDungeonPremisesCommand>
        >();

        await Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task Handle_WritesAPremise_ForEveryDungeonInTheBatch()
    {
        // Arrange — one ordinary building alongside the dungeons, to prove the batch does not stop
        // early or skip everything just because one entry does not need writing.
        var mine = await SeedBuilding(BuildingType.Mine);
        var crypt = await SeedBuilding(BuildingType.Crypt);
        var inn = await SeedBuilding(BuildingType.Inn);

        // Act
        await _handler.Handle(
            new EnsureDungeonPremisesCommand { BuildingIds = [mine.Id, crypt.Id, inn.Id] },
            TestContext.Current.CancellationToken
        );

        // Assert
        await using var verifyContext = db.CreateContext();
        var buildings = await verifyContext
            .Buildings.Where(b => b.WorldId == WorldId)
            .ToDictionaryAsync(b => b.Id, TestContext.Current.CancellationToken);
        Assert.Equal("A mine abandoned.", buildings[mine.Id].Premise);
        Assert.Equal("A mine abandoned.", buildings[crypt.Id].Premise);
        Assert.Null(buildings[inn.Id].Premise);
    }

    private async Task<Building> SeedBuilding(BuildingType buildingType)
    {
        var building = Builders.MakeBuilding(worldId: WorldId, buildingType: buildingType);
        _context.Buildings.Add(building);
        await _context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return building;
    }
}
