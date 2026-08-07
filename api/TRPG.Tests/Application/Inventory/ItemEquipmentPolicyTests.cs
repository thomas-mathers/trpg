using TRPG.Application.Inventory;
using TRPG.Data.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Inventory;

public sealed class ItemEquipmentPolicyTests
{
    [Theory]
    [InlineData(EquipmentSlot.RightHand)]
    [InlineData(EquipmentSlot.LeftHand)]
    public void GetFootprint_ReturnsBothHands_ForTwoHandedWeapon_RegardlessOfRequestedSlot(
        EquipmentSlot requestedSlot
    )
    {
        // Arrange
        var weapon = Builders.MakeWeaponItem(isTwoHanded: true);

        // Act
        var footprint = ItemEquipmentPolicy.GetFootprint(weapon, requestedSlot);

        // Assert
        Assert.Equivalent(new[] { EquipmentSlot.RightHand, EquipmentSlot.LeftHand }, footprint);
    }

    [Fact]
    public void GetFootprint_ReturnsRequestedSlotOnly_ForOneHandedItem()
    {
        // Arrange
        var weapon = Builders.MakeWeaponItem(isTwoHanded: false);

        // Act
        var footprint = ItemEquipmentPolicy.GetFootprint(weapon, EquipmentSlot.LeftHand);

        // Assert
        Assert.Equal([EquipmentSlot.LeftHand], footprint);
    }

    [Theory]
    [InlineData(EquipmentSlot.RightHand)]
    [InlineData(EquipmentSlot.LeftHand)]
    public void ResolveEquippedSlot_ReturnsRightHand_ForTwoHandedWeapon_RegardlessOfRequestedSlot(
        EquipmentSlot requestedSlot
    )
    {
        // Arrange
        var weapon = Builders.MakeWeaponItem(isTwoHanded: true);

        // Act
        var resolvedSlot = ItemEquipmentPolicy.ResolveEquippedSlot(weapon, requestedSlot);

        // Assert
        Assert.Equal(EquipmentSlot.RightHand, resolvedSlot);
    }

    [Fact]
    public void ResolveEquippedSlot_ReturnsRequestedSlot_ForOneHandedItem()
    {
        // Arrange
        var weapon = Builders.MakeWeaponItem(isTwoHanded: false);

        // Act
        var resolvedSlot = ItemEquipmentPolicy.ResolveEquippedSlot(weapon, EquipmentSlot.LeftHand);

        // Assert
        Assert.Equal(EquipmentSlot.LeftHand, resolvedSlot);
    }
}
