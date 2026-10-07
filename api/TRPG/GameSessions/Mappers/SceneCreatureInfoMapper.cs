using TRPG.Application.Scenes.Results;
using TRPG.Combat.Mappers;
using TRPG.GameSessions.Responses;

namespace TRPG.GameSessions.Mappers;

internal static class SceneCreatureInfoMapper
{
    public static CreatureStatusSnapshot ToStatusSnapshot(this SceneCreatureInfo creature) =>
        new(
            creature.Id,
            creature.Name,
            creature.CreatureType.ToResponse(),
            creature.Gender.ToResponse(),
            creature.Profession?.ToResponse(),
            creature.Level,
            creature.Age,
            creature.Condition.ToResponse(),
            creature.Activity?.ToResponse(),
            creature.Posture.ToResponse(),
            creature.Movement.ToResponse(),
            creature.IsSneaking,
            creature.IsAlerted,
            creature.IsRestrained,
            creature.Gold,
            creature.CurrentHp,
            creature.MaximumHp,
            creature.CurrentAp,
            creature.MaximumAp,
            creature.CurrentMp,
            creature.MaximumMp,
            creature.ExperienceCurrent,
            creature.ExperienceToNextLevel,
            creature.FactionNames,
            creature.Reputation,
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
            creature.MagicResistance,
            creature.TradeWorkstationId,
            creature.QuestMarkers.Select(marker => marker.ToResponse()).ToArray(),
            creature.ReadyToDeliver,
            creature.Effects.Conditions.ToContract(),
            creature.Effects.Dots.Select(dot => dot.ToContract()).ToArray(),
            creature.Effects.Hots.Select(hot => hot.ToContract()).ToArray(),
            creature.Effects.Buffs.Select(buff => buff.ToContract()).ToArray(),
            creature.Placement.ToWire()
        )
        {
            Equipment = creature
                .Equipment.Select(item => new EquippedGearSnapshot(
                    item.ItemId,
                    item.Slot.ToString(),
                    item.ModelClass
                ))
                .ToArray(),
            WalkPath = creature.WalkPath.Select(point => point.ToWire()).ToArray(),
        };
}
