using System.Transactions;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Extensions;
using TRPG.Application.Combat.Results;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Encounters.Events;
using TRPG.Application.Encounters.Fights;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class OpenFightCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid TargetId { get; init; }
    public required string AbilityName { get; init; }
    public required FightDraft Draft { get; init; }
    public required GameInstant GameTime { get; init; }
}

public record OpenFightResult(AbilityUseResult Use, ActiveFight? Fight);

internal class OpenFightCommandHandler(
    IEncountersDbContext context,
    CombatEngine combatEngine,
    ICommandHandler<WakeCreaturesCommand> wakeCreatures,
    ICommandHandler<AlertCreaturesCommand> alertCreatures,
    ICommandHandler<RecordAssaultCommand> recordAssault,
    ICommandHandler<ResolveCombatRoundCommand, CombatResult> resolveCombatRound,
    EncounterEngagementManager engagementManager,
    IGameClientEventSink gameEvents
) : ICommandHandler<OpenFightCommand, OpenFightResult>
{
    public async Task<OpenFightResult> Handle(
        OpenFightCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var combatants = command.Draft.Combatants;
        var player = combatants.Single(combatant => combatant.IsPlayer);

        player.IsSurpriseAttacker = command.Draft.HasSurpriseRound;
        var use = combatEngine.UseAbility(
            combatants,
            command.PlayerId,
            command.TargetId,
            command.AbilityName,
            command.GameTime
        );
        player.IsSurpriseAttacker = false;
        if (use.State is null)
        {
            return new OpenFightResult(use, null);
        }

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var fight = await StartFight(command, cancellationToken);
        await resolveCombatRound.Handle(
            new ResolveCombatRoundCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                LocationId = command.Draft.LocationId,
                Combatants = combatants,
                State = use.State,
                GameTime = command.GameTime,
            },
            cancellationToken
        );

        transaction.Complete();

        return new OpenFightResult(
            use,
            new ActiveFight(
                fight.Id,
                command.WorldId,
                command.PlayerId,
                command.Draft.LocationId,
                combatants
            )
        );
    }

    private async Task<FightEncounter> StartFight(
        OpenFightCommand command,
        CancellationToken cancellationToken
    )
    {
        var combatants = command.Draft.Combatants;
        var enemyIds = combatants
            .Where(combatant => !combatant.IsPlayer)
            .Select(combatant => combatant.CreatureId)
            .ToArray();

        await wakeCreatures.Handle(
            new WakeCreaturesCommand { CreatureIds = enemyIds },
            cancellationToken
        );
        await alertCreatures.Handle(
            new AlertCreaturesCommand { CreatureIds = enemyIds },
            cancellationToken
        );
        await recordAssault.Handle(
            new RecordAssaultCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                VictimId = command.TargetId,
            },
            cancellationToken
        );

        var fight = new FightEncounter
        {
            WorldId = command.WorldId,
            PlayerId = command.PlayerId,
            LocationId = command.Draft.LocationId,
            CombatantIds = combatants.Select(combatant => combatant.CreatureId).ToList(),
            HasSurpriseRound = command.Draft.HasSurpriseRound,
            PlayerWasAggressor = true,
        };
        context.Encounters.Add(fight);
        await context.SaveChangesAsync(cancellationToken);

        await engagementManager.EngageOpponents(fight, command.GameTime, cancellationToken);
        gameEvents.Enqueue(
            new CombatStartedEvent(
                command.WorldId,
                fight.Id,
                combatants.Select(combatant => combatant.ToCombatantResult()).ToArray()
            )
        );

        return fight;
    }
}
