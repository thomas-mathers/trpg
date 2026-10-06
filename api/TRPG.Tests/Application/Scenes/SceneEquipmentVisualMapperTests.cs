using TRPG.Application.Scenes.Results;
using TRPG.Domain.Models;
using TRPG.Tests.Helpers;

namespace TRPG.Tests.Application.Scenes;

public class SceneEquipmentVisualMapperTests
{
    [Fact]
    public void ToVisualEquipment_NamesArmorByClassAndType()
    {
        var armor = Builders.MakeArmor(type: ArmorType.Helm, armorClass: ArmorClass.Plate);
        armor.Ownership.EquippedSlot = EquipmentSlot.Helm;

        var visuals = new List<Item> { armor }.ToVisualEquipment();

        var visual = Assert.Single(visuals);
        Assert.Equal(new SceneEquipmentVisual(armor.Id, EquipmentSlot.Helm, "PlateHelm"), visual);
    }

    [Fact]
    public void ToVisualEquipment_NamesWeaponsByTypeAndShieldsAsShield()
    {
        var weapon = Builders.MakeWeapon(type: WeaponType.Axe);
        weapon.Ownership.EquippedSlot = EquipmentSlot.RightHand;
        var shield = Builders.MakeShield();
        shield.Ownership.EquippedSlot = EquipmentSlot.LeftHand;

        var visuals = new List<Item> { weapon, shield }.ToVisualEquipment();

        Assert.Equal(["Axe", "Shield"], visuals.Select(visual => visual.ModelClass));
    }

    [Fact]
    public void ToVisualEquipment_SkipsItemsWithoutAVisual()
    {
        var consumable = Builders.MakeConsumable();
        consumable.Ownership.EquippedSlot = EquipmentSlot.Belt;

        var visuals = new List<Item> { consumable }.ToVisualEquipment();

        Assert.Empty(visuals);
    }
}
