using TRPG.Application.Worlds.Generators;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Worlds.Generators;

public class TradeStockGeneratorTests
{
    private static readonly Guid WorldId = Guid.NewGuid();

    private readonly TradeStockGenerator _generator = new(
        new ItemGenerator(
            new WeaponGenerator(),
            new ArmorGenerator(),
            new AccessoryGenerator(),
            new ConsumableGenerator(),
            new AmmoGenerator()
        )
    );

    [Fact]
    public void Generate_AssignsGoldAndPositiveQuantities_ToEveryTradeCounter()
    {
        // Arrange
        var scenarios = Enum.GetValues<BuildingType>().Select(CreateTradeCounter).ToArray();

        // Act
        var stock = _generator.Generate(
            scenarios.Select(scenario => (Prop)scenario.Workstation).ToArray(),
            scenarios.Select(scenario => scenario.Room).ToArray(),
            scenarios.Select(scenario => scenario.Building).ToArray(),
            WorldId
        );

        // Assert
        foreach (var scenario in scenarios)
        {
            var counterStock = stock
                .Where(item => item.Ownership.OwnerId == scenario.Workstation.Id)
                .ToArray();
            var gold = Assert.Single(counterStock.OfType<Gold>());

            Assert.Equal(500, gold.Quantity);
            Assert.All(
                counterStock,
                item =>
                {
                    Assert.Equal(OwnerType.Workstation, item.Ownership.OwnerType);
                    Assert.True(item.Quantity > 0);
                }
            );
        }
    }

    [Fact]
    public void Generate_CreatesWeaponArmorShieldAndAmmo_ForBlacksmith()
    {
        // Arrange
        var scenario = CreateTradeCounter(BuildingType.Blacksmith);

        // Act
        var stock = Generate(scenario);

        // Assert
        Assert.Contains(stock, item => item is Weapon);
        Assert.Contains(stock, item => item is Armor);
        Assert.Contains(stock, item => item is Shield);
        Assert.Contains(stock, item => item is Ammunition);
    }

    [Fact]
    public void Generate_CreatesPotionStacks_ForApothecary()
    {
        // Arrange
        var scenario = CreateTradeCounter(BuildingType.Apothecary);

        // Act
        var stock = Generate(scenario);

        // Assert
        var potions = stock.OfType<Consumable>().ToArray();
        Assert.Equal(3, potions.Length);
        Assert.Equal(
            10,
            Assert.Single(potions, potion => potion.Resource == ResourceType.Hp).Quantity
        );
        Assert.Equal(
            10,
            Assert.Single(potions, potion => potion.Resource == ResourceType.Ap).Quantity
        );
        Assert.Equal(
            10,
            Assert.Single(potions, potion => potion.Resource == ResourceType.Mp).Quantity
        );
    }

    [Theory]
    [InlineData(BuildingType.Bakery)]
    [InlineData(BuildingType.Inn)]
    [InlineData(BuildingType.Tavern)]
    [InlineData(BuildingType.Library)]
    [InlineData(BuildingType.Temple)]
    [InlineData(BuildingType.Stable)]
    public void Generate_CreatesOnlyGold_WhenVenueHasNoImplementedSellableItems(
        BuildingType buildingType
    )
    {
        // Arrange
        var scenario = CreateTradeCounter(buildingType);

        // Act
        var stock = Generate(scenario);

        // Assert
        Assert.Single(stock);
        Assert.IsType<Gold>(stock.Single());
    }

    private IReadOnlyCollection<Item> Generate(TradeCounterScenario scenario) =>
        _generator.Generate([scenario.Workstation], [scenario.Room], [scenario.Building], WorldId);

    private static TradeCounterScenario CreateTradeCounter(BuildingType buildingType)
    {
        var building = Builders.MakeBuilding(
            stateId: Guid.NewGuid(),
            worldId: WorldId,
            buildingType: buildingType
        );
        var room = Builders.MakeRoom(building.Id, worldId: WorldId);
        var workstation = Builders.MakeWorkstation(worldId: WorldId, locationId: room.LocationId);

        return new TradeCounterScenario(building, room, workstation);
    }

    private sealed record TradeCounterScenario(
        Building Building,
        Room Room,
        Workstation Workstation
    );
}
