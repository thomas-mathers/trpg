using TRPG.Application.Creatures.Queries;
using TRPG.Data.Models;

namespace TRPG.Application.Creatures.Mappers;

internal static class CreatureMapper
{
    public static CreatureSummary ToSummary(
        this Creature creature,
        int gold,
        Guid stateId,
        Guid? cityId,
        Guid? districtId,
        Guid? roomId
    ) =>
        new(
            creature.Id,
            creature.Name,
            creature.CreatureType,
            creature.Gender,
            creature.Profession,
            creature.Level,
            creature.BirthYear,
            creature.State,
            gold,
            stateId,
            creature.LocationId,
            districtId,
            roomId,
            cityId,
            creature.CurrentHp,
            creature.MaximumHp,
            creature.CurrentAp,
            creature.MaximumAp,
            creature.CurrentMp,
            creature.MaximumMp,
            creature.Strength,
            creature.Dexterity,
            creature.Intelligence,
            creature.Endurance,
            creature.Stamina,
            creature.Mana,
            creature.Defense,
            creature.MovementSpeed,
            creature.PhysicalResistance,
            creature.FireResistance,
            creature.IceResistance,
            creature.LightningResistance,
            creature.PoisonResistance,
            creature.MagicResistance
        );
}
