using TRPG.Application.Combat;
using TRPG.Application.Combat.Results;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Encounters.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class AdvanceFightEffectsCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class AdvanceFightEffectsCommandHandler(
    IQueryHandler<GetActiveFightQuery, FightEncounter?> getActiveFight,
    ActiveFightCombatantLoader combatantLoader,
    CombatEngine combatEngine,
    ICommandHandler<ResolveCombatRoundCommand, CombatResult> resolveCombatRound
) : ICommandHandler<AdvanceFightEffectsCommand>
{
    public async Task Handle(
        AdvanceFightEffectsCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var fight = await getActiveFight.Handle(
            new GetActiveFightQuery { PlayerId = command.PlayerId },
            cancellationToken
        );
        if (fight == null)
        {
            return;
        }

        var combatants = await combatantLoader.Load(command.PlayerId, cancellationToken);

        var state = combatEngine.ProcessEffectTick(combatants, command.GameTime);
        if (state.Events.Count == 0)
        {
            return;
        }

        await resolveCombatRound.Handle(
            new ResolveCombatRoundCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                LocationId = fight.LocationId,
                Combatants = combatants,
                State = state,
                GameTime = command.GameTime,
            },
            cancellationToken
        );
    }
}
