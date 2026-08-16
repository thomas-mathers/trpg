using TRPG.Application.Inventory;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory;

public sealed class ItemStackabilityTests
{
    public static IEnumerable<object[]> StackableItems =>
        [
            [Builders.MakeConsumable()],
            [Builders.MakeAmmunition()],
            [Builders.MakeGold()],
            [Builders.MakeWeapon(type: WeaponType.Javelin)],
        ];

    public static IEnumerable<object[]> NonStackableItems =>
        [
            [Builders.MakeWeapon(type: WeaponType.Sword)],
            [Builders.MakeArmor()],
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
