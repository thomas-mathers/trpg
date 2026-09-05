using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Combat;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters.Results;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class ResolveHostileEncounterActionCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required HostileEncounterAction Action { get; init; }
    public required Guid EncounterId { get; init; }
}

internal class ResolveHostileEncounterActionCommandHandler(
    IEncountersDbContext context,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    IQueryHandler<GetCreaturesByIdsQuery, IReadOnlyDictionary<Guid, Creature>> getCreaturesByIds,
    ICommandHandler<CompleteEncounterCommand> completeEncounter,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    ICommandHandler<StartFightCommand> startFight,
    IOptionsSnapshot<FleeOptions> fleeOptions
) : ICommandHandler<ResolveHostileEncounterActionCommand, HostileEncounterActionResult>
{
    public async Task<HostileEncounterActionResult> Handle(
        ResolveHostileEncounterActionCommand command,
        CancellationToken cancellationToken = default
    )
    {
        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var encounter = await GetEncounter(command, cancellationToken);

        await completeEncounter.Handle(
            new CompleteEncounterCommand { EncounterId = command.EncounterId },
            cancellationToken
        );

        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );
        if (player == null)
        {
            throw new EntityNotFoundException(nameof(Creature), command.PlayerId);
        }

        var memberIds = encounter.Members.Select(member => member.Id).ToArray();
        var membersById = await getCreaturesByIds.Handle(
            new GetCreaturesByIdsQuery { Ids = memberIds },
            cancellationToken
        );
        var missingMemberIds = memberIds.Where(id => !membersById.ContainsKey(id)).ToArray();
        if (missingMemberIds.Length > 0)
        {
            throw new EntityNotFoundException(nameof(Creature), missingMemberIds[0]);
        }

        var actionKind = ToActionKind(command.Action);
        var outcome = HostileEncounterActionResolver.Resolve(
            actionKind,
            fleeOptions.Value,
            ToEvadeParticipant(player),
            memberIds.Select(id => ToEvadeParticipant(membersById[id])).ToArray(),
            Random.Shared.NextDouble()
        );

        await ApplyEncounterOutcome(outcome, command, encounter, player, cancellationToken);

        var fact = new HostileEncounterResolutionFact(
            EncounterId: command.EncounterId,
            Outcome: ToResolutionOutcome(outcome),
            FactionName: encounter.FactionName,
            LocationName: encounter.LocationName!,
            MemberNames: encounter.Members.Select(member => member.Name).ToArray()
        );

        transaction.Complete();

        return new HostileEncounterActionResult(actionKind, fact);
    }

    private async Task ApplyEncounterOutcome(
        HostileEncounterActionOutcome outcome,
        ResolveHostileEncounterActionCommand command,
        HostileEncounter encounter,
        Creature player,
        CancellationToken cancellationToken
    )
    {
        if (
            outcome == HostileEncounterActionOutcome.Retreated
            && player.PreviousLocationId is { } originLocationId
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
            return;
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
            return;
        }

        var enemyCreatureIds = encounter.Members.Select(member => member.Id).ToArray();

        await updateCreatures.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = enemyCreatureIds,
                State = CreatureState.Alerted,
            },
            cancellationToken
        );

        await startFight.Handle(
            new StartFightCommand
            {
                SessionId = command.SessionId,
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                EnemyCreatureIds = enemyCreatureIds,
                HasSurpriseRound = false,
            },
            cancellationToken
        );
    }

    private async Task<HostileEncounter> GetEncounter(
        ResolveHostileEncounterActionCommand command,
        CancellationToken cancellationToken
    )
    {
        var encounter = await context
            .Encounters.OfType<HostileEncounter>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item =>
                    item.Id == command.EncounterId
                    && item.WorldId == command.WorldId
                    && item.PlayerId == command.PlayerId,
                cancellationToken
            );
        if (encounter == null)
        {
            throw new EntityNotFoundException(nameof(HostileEncounter), command.EncounterId);
        }
        if (encounter.State != EncounterState.Active)
        {
            throw new InvalidOperationException("The hostile encounter has already been resolved.");
        }

        return encounter;
    }

    private static EvadeParticipant ToEvadeParticipant(Creature creature) =>
        new(
            creature.Dexterity,
            creature.CurrentHp,
            creature.MaximumHp,
            creature.CurrentAp,
            creature.MaximumAp
        );

    private static HostileEncounterActionKind ToActionKind(HostileEncounterAction action) =>
        action switch
        {
            AttackEncounterAction => HostileEncounterActionKind.Attack,
            EvadeEncounterAction => HostileEncounterActionKind.Evade,
            RetreatEncounterAction => HostileEncounterActionKind.Retreat,
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };

    private static HostileEncounterResolutionOutcome ToResolutionOutcome(
        HostileEncounterActionOutcome outcome
    ) =>
        outcome switch
        {
            HostileEncounterActionOutcome.Attacked => HostileEncounterResolutionOutcome.Attacked,
            HostileEncounterActionOutcome.Evaded => HostileEncounterResolutionOutcome.Evaded,
            HostileEncounterActionOutcome.EvadeFailed =>
                HostileEncounterResolutionOutcome.EvadeFailed,
            HostileEncounterActionOutcome.Retreated => HostileEncounterResolutionOutcome.Retreated,
            HostileEncounterActionOutcome.RetreatFailed =>
                HostileEncounterResolutionOutcome.RetreatFailed,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };
}
