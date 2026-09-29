using TRPG.Application.CreatureFormulas;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Mappers;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Effects;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures;

internal sealed record AdvancedCreatureEffects(CreatureStateUpdate Update, CreatureVitals Vitals);

internal sealed class CreatureEffectAdvancer(EffectAdvancer advancer)
{
    public AdvancedCreatureEffects? Advance(
        Creature creature,
        IReadOnlyList<Item> equippedItems,
        GameInstant now
    )
    {
        var initialAttributes = StatFormulas.CalculateEffectiveAttributes(
            creature.BaseAttributes,
            [],
            equippedItems
        );
        var state = creature.ToEffectState(equippedItems);
        state.CurrentHp = Math.Clamp(creature.CurrentHp, 0, initialAttributes.MaximumHp);
        var result = advancer.Advance(state, now);
        if (!result.Changed)
        {
            return null;
        }

        var attributes = StatFormulas.CalculateEffectiveAttributes(
            creature.BaseAttributes,
            state.ActiveBuffs,
            equippedItems
        );
        var vitals = new CreatureVitals(
            CreatureId: creature.Id,
            CurrentHp: state.CurrentHp,
            MaximumHp: attributes.MaximumHp,
            CurrentAp: Math.Min(creature.CurrentAp, initialAttributes.MaximumAp),
            MaximumAp: attributes.MaximumAp,
            CurrentMp: Math.Min(creature.CurrentMp, initialAttributes.MaximumMp),
            MaximumMp: attributes.MaximumMp
        );
        return new AdvancedCreatureEffects(state.ToCreatureStateUpdate(vitals), vitals);
    }
}
