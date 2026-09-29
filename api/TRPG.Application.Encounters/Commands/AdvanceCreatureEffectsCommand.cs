using TRPG.Application.Combat;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Creatures.Results;
using TRPG.Application.Encounters.Mappers;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class AdvanceCreatureEffectsCommand
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<Guid> CreatureIds { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class AdvanceCreatureEffectsCommandHandler(
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    CombatantFactory combatantFactory,
    EffectAdvancer effectAdvancer,
    ICommandHandler<PersistCombatantsCommand> persistCombatants
) : ICommandHandler<AdvanceCreatureEffectsCommand, IReadOnlyCollection<CreatureVitals>>
{
    public async Task<IReadOnlyCollection<CreatureVitals>> Handle(
        AdvanceCreatureEffectsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var creaturesById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = command.CreatureIds },
            cancellationToken
        );
        var affectedCreatures = creaturesById
            .Values.Where(creature =>
                creature.Condition != CreatureCondition.Dead && creature.HasActiveEffects
            )
            .ToArray();
        if (affectedCreatures.Length == 0)
        {
            return [];
        }

        var combatants = await combatantFactory.CreateMany(
            command.WorldId,
            affectedCreatures,
            playerId: Guid.Empty,
            cancellationToken
        );

        var changedCombatants = combatants
            .Where(combatant => AdvanceAndReportChange(combatant, command.GameTime))
            .ToArray();
        if (changedCombatants.Length == 0)
        {
            return [];
        }

        await persistCombatants.Handle(
            new PersistCombatantsCommand
            {
                Updates = changedCombatants
                    .Select(combatant => combatant.ToCreatureCombatStateUpdate())
                    .ToArray(),
            },
            cancellationToken
        );

        return changedCombatants.Select(combatant => combatant.ToVitals()).ToArray();
    }

    private bool AdvanceAndReportChange(Combatant combatant, GameInstant now)
    {
        var effectCountBefore = CountEffects(combatant);
        var events = effectAdvancer.Advance(combatant, now);

        return events.Count > 0 || CountEffects(combatant) != effectCountBefore;
    }

    private static int CountEffects(Combatant combatant) =>
        combatant.ActiveConditions.Count
        + combatant.CooldownReadyAtByAbility.Count
        + combatant.ActiveDots.Count
        + combatant.ActiveHots.Count
        + combatant.ActiveBuffs.Count;
}
