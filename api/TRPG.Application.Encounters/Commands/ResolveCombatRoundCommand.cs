using System.Transactions;
using TRPG.Application.Combat;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Combat.Mappers;
using TRPG.Application.Combat.Results;
using TRPG.Application.Common.Commands;
using TRPG.Application.Creatures.Commands;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

internal class ResolveCombatRoundCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid LocationId { get; init; }
    public required IReadOnlyList<Combatant> Combatants { get; init; }
    public required CombatState State { get; init; }
    public GameInstant GameTime { get; init; } = GameClock.Epoch;
}

internal class ResolveCombatRoundCommandHandler(
    ICommandHandler<PersistCreatureStatesCommand> persistCreatureStates,
    ICommandHandler<EndFightCommand> endFight,
    ICommandHandler<ApplyCombatRoundOutcomeCommand> applyCombatRoundOutcome
) : ICommandHandler<ResolveCombatRoundCommand, CombatResult>
{
    public async Task<CombatResult> Handle(
        ResolveCombatRoundCommand command,
        CancellationToken cancellationToken = default
    )
    {
        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );
        var state = command.State;

        await persistCreatureStates.Handle(
            new PersistCreatureStatesCommand
            {
                Updates = command
                    .Combatants.Select(combatant => combatant.ToCreatureStateUpdate())
                    .ToArray(),
            },
            cancellationToken
        );

        var isFightEnding =
            state.Outcome is CombatOutcome.Victory or CombatOutcome.Defeat or CombatOutcome.Fled;

        if (isFightEnding)
        {
            await endFight.Handle(
                new EndFightCommand
                {
                    WorldId = command.WorldId,
                    State = state,
                    GameTime = command.GameTime,
                },
                cancellationToken
            );
        }

        await applyCombatRoundOutcome.Handle(
            new ApplyCombatRoundOutcomeCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                LocationId = command.LocationId,
                Combatants = command.Combatants,
                State = state,
            },
            cancellationToken
        );

        transaction.Complete();
        return state.ToCombatResult();
    }
}
