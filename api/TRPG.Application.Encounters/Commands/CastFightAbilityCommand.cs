using TRPG.Application.Combat;
using TRPG.Application.Combat.Results;
using TRPG.Application.Common.Commands;
using TRPG.Application.Encounters.Fights;
using TRPG.Domain;

namespace TRPG.Application.Encounters.Commands;

public class CastFightAbilityCommand
{
    public required ActiveFight Fight { get; init; }
    public required Guid TargetId { get; init; }
    public required string AbilityName { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class CastFightAbilityCommandHandler(
    CombatEngine combatEngine,
    ICommandHandler<ResolveCombatRoundCommand, CombatResult> resolveCombatRound
) : ICommandHandler<CastFightAbilityCommand, AbilityUseResult>
{
    public async Task<AbilityUseResult> Handle(
        CastFightAbilityCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var fight = command.Fight;
        var use = combatEngine.UseAbility(
            fight.Combatants,
            fight.PlayerId,
            command.TargetId,
            command.AbilityName,
            command.GameTime
        );
        if (use.State is null)
        {
            return use;
        }

        await resolveCombatRound.Handle(
            new ResolveCombatRoundCommand
            {
                WorldId = fight.WorldId,
                PlayerId = fight.PlayerId,
                LocationId = fight.LocationId,
                Combatants = fight.Combatants,
                State = use.State,
                GameTime = command.GameTime,
            },
            cancellationToken
        );

        return use;
    }
}
