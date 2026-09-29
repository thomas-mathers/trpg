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
            creature.ReadyToDeliver
        );
}
