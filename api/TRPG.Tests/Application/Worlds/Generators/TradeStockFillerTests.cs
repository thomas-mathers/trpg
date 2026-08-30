using TRPG.Application.Worlds.Generators;
using TRPG.Domain.Models;

namespace TRPG.Tests.Application.Worlds.Generators;

public class TradeStockFillerTests
{
    private readonly Guid _worldId = Guid.NewGuid();
    private readonly ItemGenerator _itemGenerator = new(
        new WeaponGenerator(),
        new ArmorGenerator(),
        new AccessoryGenerator(),
        new ConsumableGenerator(),
        new AmmoGenerator()
    );

    [Fact]
    public void Fill_AddsGold_WhenNonePresent()
    {
        // Act
        var result = TradeStockFiller.Fill(
            _itemGenerator,
            BuildingType.Blacksmith,
            currentItems: [],
            _worldId,
            playerLevel: 5
        );

        // Assert
        var gold = Assert.Single(result.ItemsToAdd.OfType<Gold>());
        Assert.Equal(500, gold.Quantity);
    }

    [Fact]
    public void Fill_TopsUpGold_WhenBelowStartingAmount()
    {
        // Arrange
        var existingGold = new Gold
        {
            WorldId = _worldId,
            Name = "Gold",
            Quantity = 100,
        };

        // Act
        var result = TradeStockFiller.Fill(
            _itemGenerator,
            BuildingType.Blacksmith,
            currentItems: [existingGold],
            _worldId,
            playerLevel: 5
        );

        // Assert
        Assert.Empty(result.ItemsToAdd.OfType<Gold>());
        Assert.Equal(500, result.QuantityIncreasesByItemId[existingGold.Id]);
    }

    [Fact]
    public void Fill_DoesNotAddAnotherWeapon_WhenAllWeaponSlotsAreAlreadyFilled()
    {
        // Arrange — Blacksmith has 3 weapon slots
        var currentItems = Enumerable
            .Range(0, 3)
            .Select(_ => (Item)new Weapon { WorldId = _worldId, Type = WeaponType.Sword })
            .ToArray();

        // Act
        var result = TradeStockFiller.Fill(
            _itemGenerator,
            BuildingType.Blacksmith,
            currentItems,
            _worldId,
            playerLevel: 5
        );

        // Assert
        Assert.Empty(result.ItemsToAdd.OfType<Weapon>());
    }

    [Fact]
    public void Fill_AddsMissingWeapons_UpToTheSlotCount()
    {
        // Act — Blacksmith has 3 weapon slots, none currently present
        var result = TradeStockFiller.Fill(
            _itemGenerator,
            BuildingType.Blacksmith,
            currentItems: [],
            _worldId,
            playerLevel: 5
        );

        // Assert
        Assert.Equal(3, result.ItemsToAdd.OfType<Weapon>().Count());
    }

    [Fact]
    public void Fill_ScalesNewUniqueGearLevel_AroundThePlayerLevel()
    {
        // Act
        var result = TradeStockFiller.Fill(
            _itemGenerator,
            BuildingType.Blacksmith,
            currentItems: [],
            _worldId,
            playerLevel: 20
        );

        // Assert
        Assert.All(
            result.ItemsToAdd.OfType<Weapon>(),
            weapon => Assert.InRange(weapon.Level, 18, 22)
        );
    }

    [Fact]
    public void Fill_AddsAllThreePotionTypes_WhenNonePresent()
    {
        // Act
        var result = TradeStockFiller.Fill(
            _itemGenerator,
            BuildingType.Apothecary,
            currentItems: [],
            _worldId,
            playerLevel: 5
        );

        // Assert
        var potions = result.ItemsToAdd.OfType<Consumable>().ToArray();
        Assert.Equal(3, potions.Length);
        Assert.All(potions, potion => Assert.Equal(10, potion.Quantity));
    }

    [Fact]
    public void Fill_TopsUpAnExistingPotionStack_ToItsOriginalQuantity()
    {
        // Arrange
        var existingPotion = new Consumable
        {
            WorldId = _worldId,
            Resource = ResourceType.Hp,
            Quantity = 2,
        };

        // Act
        var result = TradeStockFiller.Fill(
            _itemGenerator,
            BuildingType.Apothecary,
            currentItems: [existingPotion],
            _worldId,
            playerLevel: 5
        );

        // Assert
        Assert.Equal(10, result.QuantityIncreasesByItemId[existingPotion.Id]);
        Assert.DoesNotContain(
            result.ItemsToAdd.OfType<Consumable>(),
            c => c.Resource == ResourceType.Hp
        );
    }

    [Fact]
    public void Fill_DoesNotTopUpAPotionStack_ThatAlreadyMeetsTheOriginalQuantity()
    {
        // Arrange
        var existingPotion = new Consumable
        {
            WorldId = _worldId,
            Resource = ResourceType.Hp,
            Quantity = 10,
        };

        // Act
        var result = TradeStockFiller.Fill(
            _itemGenerator,
            BuildingType.Apothecary,
            currentItems: [existingPotion],
            _worldId,
            playerLevel: 5
        );

        // Assert
        Assert.DoesNotContain(existingPotion.Id, result.QuantityIncreasesByItemId.Keys);
    }

    [Fact]
    public void Fill_ReturnsOnlyGold_ForABuildingTypeWithNoStockCatalog()
    {
        // Act
        var result = TradeStockFiller.Fill(
            _itemGenerator,
            BuildingType.Tavern,
            currentItems: [],
            _worldId,
            playerLevel: 5
        );

        // Assert
        var item = Assert.Single(result.ItemsToAdd);
        Assert.IsType<Gold>(item);
    }
}
