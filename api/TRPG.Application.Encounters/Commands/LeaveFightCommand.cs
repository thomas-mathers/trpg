using TRPG.Application.Combat;
using TRPG.Application.Combat.Results;
using TRPG.Application.Common.Commands;
using TRPG.Application.Encounters.Fights;
using TRPG.Domain;

namespace TRPG.Application.Encounters.Commands;

public class LeaveFightCommand
{
    public required ActiveFight Fight { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class LeaveFightCommandHandler(
    CombatEngine combatEngine,
    ICommandHandler<ResolveCombatRoundCommand, CombatResult> resolveCombatRound
) : ICommandHandler<LeaveFightCommand>
{
    public async Task Handle(
        LeaveFightCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var fight = command.Fight;

        await resolveCombatRound.Handle(
            new ResolveCombatRoundCommand
            {
                WorldId = fight.WorldId,
                PlayerId = fight.PlayerId,
                LocationId = fight.LocationId,
                Combatants = fight.Combatants,
                State = combatEngine.Flee(fight.Combatants),
                GameTime = command.GameTime,
            },
            cancellationToken
        );
    }
}
