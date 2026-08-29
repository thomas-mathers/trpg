using TRPG.Application.Combat.Results;
using TRPG.Application.Common.Commands;
using TRPG.Application.Creatures.Commands;
using TRPG.Domain.Models;

namespace TRPG.Application.Combat.Commands;

public class ResolvePlayerCombatActionCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required PlayerCombatAction Action { get; init; }
}

public record PlayerCombatActionResult(
    CombatResult CombatResult,
    IReadOnlyCollection<string> OpponentNames
);

internal class ResolvePlayerCombatActionCommandHandler(
    ICommandHandler<
        ApplyPassiveRegenCommand,
        IReadOnlyDictionary<Guid, Creature>
    > applyPassiveRegen,
    ActiveFightCombatantLoader combatantLoader,
    CombatEngine combatEngine,
    ICommandHandler<ResolveCombatRoundCommand, CombatResult> resolveCombatRound
) : ICommandHandler<ResolvePlayerCombatActionCommand, PlayerCombatActionResult>
{
    public async Task<PlayerCombatActionResult> Handle(
        ResolvePlayerCombatActionCommand command,
        CancellationToken cancellationToken = default
    )
    {
        await applyPassiveRegen.Handle(
            new ApplyPassiveRegenCommand
            {
                SessionId = command.SessionId,
                CreatureIds = [command.PlayerId],
            },
            cancellationToken
        );
        var combatants = await combatantLoader.Load(command.PlayerId, cancellationToken);
        if (combatants.Count == 0)
            throw new InvalidOperationException("There's no fight to act in right now.");
        var resolved = new PlayerCombatActionResolver(combatants).Resolve(command.Action);
        if (resolved.ErrorMessage is not null)
            throw new InvalidOperationException(resolved.ErrorMessage);
        var state = combatEngine.ProcessRound(combatants, resolved.Result!);
        var combatResult = await resolveCombatRound.Handle(
            new ResolveCombatRoundCommand
            {
                SessionId = command.SessionId,
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                Combatants = combatants,
                State = state,
            },
            cancellationToken
        );

        var opponentNames = combatants
            .Where(combatant => !combatant.IsPlayer)
            .Select(combatant => combatant.Name)
            .ToArray();

        return new PlayerCombatActionResult(combatResult, opponentNames);
    }
}
