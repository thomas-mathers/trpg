using TRPG.Application.Inventory;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory;

public sealed class ItemStackabilityTests
{
    public static IEnumerable<object[]> StackableItems =>
        [
            [Builders.MakeConsumableItem()],
            [Builders.MakeAmmunitionItem()],
            [Builders.MakeGold()],
            [Builders.MakeWeaponItem(type: WeaponType.Javelin)],
        ];

    public static IEnumerable<object[]> NonStackableItems =>
        [
            [Builders.MakeWeaponItem(type: WeaponType.Sword)],
            [Builders.MakeArmorItem()],
        ];

    [Theory]
    [MemberData(nameof(StackableItems))]
    public void IsStackable_ReturnsTrue_ForStackableItem(Item item)
    {
        // Act
        var isStackable = ItemStackability.IsStackable(item);

        // Assert
        Assert.True(isStackable);
    }

    [Theory]
    [MemberData(nameof(NonStackableItems))]
    public void IsStackable_ReturnsFalse_ForNonStackableItem(Item item)
    {
        // Act
        var isStackable = ItemStackability.IsStackable(item);

        // Assert
        Assert.False(isStackable);
    }
}
