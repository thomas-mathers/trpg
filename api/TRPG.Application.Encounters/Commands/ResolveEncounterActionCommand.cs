using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Mappers;
using TRPG.Application.Common.Handling;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Contracts.Combat.Responses;
using TRPG.Contracts.Encounters.Requests;
using TRPG.Contracts.Encounters.Responses;
using TRPG.Data.Models;
using Combatant = TRPG.Application.Combat.Combatant;

namespace TRPG.Application.Encounters.Commands;

public class ResolveEncounterActionCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required PlayerEncounterAction Action { get; init; }
    public required Guid EncounterId { get; init; }
    public required Guid EncounterGroupId { get; init; }
    public required Guid EncounterLocationId { get; init; }
    public Guid? ArrivalOriginLocationId { get; init; }
}

public record EncounterActionResolution(
    HostileEncounterActionKind ActionKind,
    EncounterResolutionFact Fact,
    IReadOnlyCollection<CombatantState>? Combatants
);

internal class ResolveEncounterActionCommandHandler(
    IQueryHandler<GetEncounterGroupContextQuery, EncounterGroupContext> getEncounterGroupContext,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    ICommandHandler<CompleteEncounterCommand> completeEncounter,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    ICommandHandler<StartFightCommand, IReadOnlyList<Combatant>> startFight,
    IQueryHandler<GetLocationByIdQuery, Location?> getLocationById
) : ICommandHandler<ResolveEncounterActionCommand, EncounterActionResolution>
{
    public async Task<EncounterActionResolution> Handle(
        ResolveEncounterActionCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var groupContext = await getEncounterGroupContext.Handle(
            new GetEncounterGroupContextQuery { EncounterGroupId = command.EncounterGroupId },
            cancellationToken
        );
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );

        var groupPower = groupContext.LivingMembers.Sum(member => member.Level);
        var actionKind = ToActionKind(command.Action);
        var outcome = HostileEncounterActionResolver.Resolve(
            actionKind,
            player!.Level,
            groupPower,
            Random.Shared.Next(100)
        );

        await completeEncounter.Handle(
            new CompleteEncounterCommand { EncounterId = command.EncounterId },
            cancellationToken
        );

        var effects = await ApplyEncounterOutcome(
            outcome,
            command,
            player,
            groupContext,
            cancellationToken
        );

        var location = await getLocationById.Handle(
            new GetLocationByIdQuery { Id = command.EncounterLocationId },
            cancellationToken
        );

        var fact = new EncounterResolutionFact(
            EncounterId: command.EncounterId,
            Outcome: ToResolutionOutcome(outcome),
            FactionName: groupContext.Faction.Name,
            LocationName: location!.Name,
            MemberNames: groupContext.LivingMembers.Select(member => member.Name).ToArray()
        );

        return new EncounterActionResolution(actionKind, fact, effects.Combatants);
    }

    private async Task<EncounterOutcomeEffects> ApplyEncounterOutcome(
        HostileEncounterActionOutcome outcome,
        ResolveEncounterActionCommand command,
        Creature player,
        EncounterGroupContext groupContext,
        CancellationToken cancellationToken
    )
    {
        if (
            outcome == HostileEncounterActionOutcome.Retreated
            && command.ArrivalOriginLocationId is { } originLocationId
        )
        {
            await updateCreatures.Handle(
                new UpdateCreaturesCommand
                {
                    CreatureIds = [player.Id],
                    LocationId = originLocationId,
                },
                cancellationToken
            );
            return new EncounterOutcomeEffects(null);
        }

        var startsFight = outcome switch
        {
            HostileEncounterActionOutcome.EvadeFailed
            or HostileEncounterActionOutcome.RetreatFailed
            or HostileEncounterActionOutcome.Attacked => true,
            _ => false,
        };
        if (!startsFight)
        {
            return new EncounterOutcomeEffects(null);
        }

        var enemyCreatureIds = groupContext.LivingMembers.Select(member => member.Id).ToArray();

        await updateCreatures.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = enemyCreatureIds,
                State = CreatureState.Alerted,
            },
            cancellationToken
        );

        var combatants = await startFight.Handle(
            new StartFightCommand
            {
                SessionId = command.SessionId,
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                EnemyCreatureIds = enemyCreatureIds,
                EncounterId = command.EncounterId,
            },
            cancellationToken
        );

        return new EncounterOutcomeEffects(CombatantStateMapper.ToCombatantStates(combatants));
    }

    private static HostileEncounterActionKind ToActionKind(PlayerEncounterAction action) =>
        action switch
        {
            AttackEncounterAction => HostileEncounterActionKind.Attack,
            EvadeEncounterAction => HostileEncounterActionKind.Evade,
            RetreatEncounterAction => HostileEncounterActionKind.Retreat,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

    private static EncounterResolutionOutcome ToResolutionOutcome(
        HostileEncounterActionOutcome outcome
    ) =>
        outcome switch
        {
            HostileEncounterActionOutcome.Attacked => EncounterResolutionOutcome.Attacked,
            HostileEncounterActionOutcome.Evaded => EncounterResolutionOutcome.Evaded,
            HostileEncounterActionOutcome.EvadeFailed => EncounterResolutionOutcome.EvadeFailed,
            HostileEncounterActionOutcome.Retreated => EncounterResolutionOutcome.Retreated,
            HostileEncounterActionOutcome.RetreatFailed => EncounterResolutionOutcome.RetreatFailed,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };
}

public record EncounterOutcomeEffects(IReadOnlyCollection<CombatantState>? Combatants);
