using TRPG.Application.GameTurns.Results;
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
            creature.State?.ToResponse(),
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
            creature.QuestMarker.ToResponse()
        );
}
