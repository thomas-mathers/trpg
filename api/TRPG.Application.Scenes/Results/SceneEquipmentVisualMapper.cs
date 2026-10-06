using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Results;

internal static class SceneEquipmentVisualMapper
{
    public static IReadOnlyCollection<SceneEquipmentVisual> ToVisualEquipment(
        this IReadOnlyList<Item> items
    ) =>
        items
            .Select(item =>
                item switch
                {
                    Armor armor => new SceneEquipmentVisual(
                        item.Id,
                        item.Ownership.EquippedSlot!.Value,
                        $"{armor.ArmorClass}{armor.Type}"
                    ),
                    Weapon weapon => new SceneEquipmentVisual(
                        item.Id,
                        item.Ownership.EquippedSlot!.Value,
                        weapon.Type.ToString()
                    ),
                    Shield => new SceneEquipmentVisual(
                        item.Id,
                        item.Ownership.EquippedSlot!.Value,
                        "Shield"
                    ),
                    _ => null,
                }
            )
            .OfType<SceneEquipmentVisual>()
            .ToArray();
}
