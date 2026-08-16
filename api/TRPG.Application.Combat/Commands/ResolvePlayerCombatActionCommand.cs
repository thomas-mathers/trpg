using TRPG.Application.Combat;
using TRPG.Application.Combat.Queries;
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

internal class ResolvePlayerCombatActionCommandHandler(
    ICommandHandler<
        ApplyPassiveRegenCommand,
        IReadOnlyDictionary<Guid, Creature>
    > applyPassiveRegen,
    ActiveFightCombatantLoader combatantLoader,
    CombatEngine combatEngine,
    ICommandHandler<ResolveCombatRoundCommand, CombatResult> resolveCombatRound
) : ICommandHandler<ResolvePlayerCombatActionCommand>
{
    public async Task Handle(
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
        await resolveCombatRound.Handle(
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
    }
}
