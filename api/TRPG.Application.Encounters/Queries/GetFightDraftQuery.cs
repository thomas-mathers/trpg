using TRPG.Application.Abilities;
using TRPG.Application.Combat;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Fights;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Queries;

public class GetFightDraftQuery
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid TargetId { get; init; }
    public required string AbilityName { get; init; }
}

internal class GetFightDraftQueryHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    IQueryHandler<
        GetEncounterGroupCreatureIdsQuery,
        IReadOnlyCollection<Guid>
    > getEncounterGroupCreatureIds,
    CombatantFactory combatantFactory
) : IQueryHandler<GetFightDraftQuery, FightDraft?>
{
    public async Task<FightDraft?> Handle(
        GetFightDraftQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = query.PlayerId },
            cancellationToken
        );
        if (player is not { IsEngaged: false, Condition: not CreatureCondition.Dead })
        {
            return null;
        }

        var playerCombatant = await combatantFactory.Create(player, true, cancellationToken);
        var ability = playerCombatant.Abilities.FirstOrDefault(a => a.Name == query.AbilityName);
        if (ability is not AttackAbility)
        {
            return await DraftSupport(player, playerCombatant, query, cancellationToken);
        }

        return await DraftFight(player, query, cancellationToken);
    }

    private async Task<FightDraft?> DraftSupport(
        Creature player,
        Combatant playerCombatant,
        GetFightDraftQuery query,
        CancellationToken cancellationToken
    )
    {
        playerCombatant.Position = new Point(player.X, player.Y);
        playerCombatant.Heading = player.Angle;
        if (query.TargetId == player.Id)
        {
            return new FightDraft(player.LocationId, [playerCombatant], false, false);
        }

        var target = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = query.TargetId },
            cancellationToken
        );
        if (target is null || target.LocationId != player.LocationId)
        {
            return null;
        }

        var targetCombatant = await combatantFactory.Create(target, false, cancellationToken);
        targetCombatant.Position = new Point(target.X, target.Y);
        targetCombatant.Heading = target.Angle;

        return new FightDraft(player.LocationId, [playerCombatant, targetCombatant], false, false);
    }

    private async Task<FightDraft?> DraftFight(
        Creature player,
        GetFightDraftQuery query,
        CancellationToken cancellationToken
    )
    {
        var target = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = query.TargetId },
            cancellationToken
        );
        if (!IsAttackable(player, target))
        {
            return null;
        }

        var groupIds = await getEncounterGroupCreatureIds.Handle(
            new GetEncounterGroupCreatureIdsQuery
            {
                WorldId = query.WorldId,
                CreatureId = query.TargetId,
            },
            cancellationToken
        );
        var enemyIds = groupIds.Append(query.TargetId).Distinct().ToArray();
        var creaturesById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = [player.Id, .. enemyIds] },
            cancellationToken
        );
        var creatures = creaturesById.Values.ToArray();
        var combatants = await combatantFactory.CreateMany(
            query.WorldId,
            creatures,
            player.Id,
            cancellationToken
        );

        var hasSurpriseRound = player.IsSneaking || target!.Condition == CreatureCondition.Sleeping;
        foreach (var combatant in combatants)
        {
            var creature = creaturesById[combatant.CreatureId];
            combatant.Position = new Point(creature.X, creature.Y);
            combatant.Heading = creature.Angle;
        }

        return new FightDraft(player.LocationId, combatants, true, hasSurpriseRound);
    }

    private static bool IsAttackable(Creature player, Creature? target) =>
        target is { Condition: not CreatureCondition.Dead, IsRestrained: false }
        && target.Id != player.Id
        && target.LocationId == player.LocationId;
}
