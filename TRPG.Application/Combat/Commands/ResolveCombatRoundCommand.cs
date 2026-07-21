using TRPG.Application.WeaponProficiency.Commands;
using TRPG.Data.Models;

namespace TRPG.Application.Combat.Commands;

internal class ResolveCombatRoundCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required IReadOnlyList<Combatant> Combatants { get; init; }
    public required CombatState State { get; init; }
}

internal class ResolveCombatRoundCommandHandler(
    PersistCombatantsCommandHandler persistCombatants,
    AdjustWeaponProficienciesCommandHandler adjustWeaponProficiencies,
    EndFightCommandHandler endFight
)
{
    public async Task<CombatResult> Handle(
        ResolveCombatRoundCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var state = command.State;

        await persistCombatants.Handle(
            new PersistCombatantsCommand { Combatants = command.Combatants },
            cancellationToken
        );

        if (state.WeaponSwingCounts.Count > 0)
        {
            await adjustWeaponProficiencies.Handle(
                new AdjustWeaponProficienciesCommand
                {
                    WorldId = command.WorldId,
                    CreatureId = command.PlayerId,
                    ProficiencyDeltas = state.WeaponSwingCounts,
                },
                cancellationToken
            );
        }

        if (state.Outcome is CombatOutcome.Victory or CombatOutcome.Defeat or CombatOutcome.Fled)
        {
            await endFight.Handle(
                new EndFightCommand
                {
                    SessionId = command.SessionId,
                    WorldId = command.WorldId,
                    State = state,
                },
                cancellationToken
            );
        }

        return state.ToCombatResult();
    }
}
