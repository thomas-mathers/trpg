using TRPG.Application.Abilities;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Effects;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Mappers;

internal static class CreatureMapper
{
    public static EffectState ToEffectState(
        this Creature creature,
        IReadOnlyList<Item> equippedItems
    ) =>
        new()
        {
            Attributes = creature.BaseAttributes,
            EquippedItems = equippedItems,
            CurrentHp = creature.CurrentHp,
            ActiveConditions = creature.ActiveConditions.ToDictionary(
                pair => Enum.Parse<ConditionType>(pair.Key),
                pair => pair.Value
            ),
            CooldownReadyAtByAbility = new Dictionary<string, GameInstant>(
                creature.CooldownReadyAtByAbility
            ),
            ActiveDots = creature.ActiveDots.Select(effect => effect.ToState()).ToList(),
            ActiveHots = creature.ActiveHots.Select(effect => effect.ToState()).ToList(),
            ActiveBuffs = creature.ActiveBuffs.Select(effect => effect.ToState()).ToList(),
        };

    public static CreatureVitals ToVitals(this Creature creature) =>
        new(
            creature.Id,
            creature.CurrentHp,
            creature.MaximumHp,
            creature.CurrentAp,
            creature.MaximumAp,
            creature.CurrentMp,
            creature.MaximumMp
        );

    public static CreatureResult ToResult(
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
            creature.Condition,
            creature.Activity,
            creature.Posture,
            creature.Movement,
            creature.PlayerCorpseOwnerId,
            creature.IsSneaking,
            creature.IsAlerted,
            creature.IsRestrained,
            gold,
            stateId,
            creature.LocationId,
            creature.PreviousLocationId,
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
            creature.MagicResistance,
            creature.ToEffects(),
            creature.X,
            creature.Y,
            creature.Angle
        );
}
