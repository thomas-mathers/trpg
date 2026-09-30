using System.Transactions;
using TRPG.Application.Combat.Mappers;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Combat.Commands;

public class CastAbilityCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid TargetId { get; init; }
    public required string AbilityName { get; init; }
    public required GameInstant GameTime { get; init; }
}

public record CastAbilityResult(
    string AbilityName,
    string AbilityDescription,
    string TargetName,
    bool TargetIsPlayer
);

internal class CastAbilityCommandHandler(
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    CombatantFactory combatantFactory,
    CombatEngine combatEngine,
    ICommandHandler<PersistCreatureStatesCommand> persistCreatureStates,
    ICommandHandler<ApplyCombatUsageCommand> applyCombatUsage
) : ICommandHandler<CastAbilityCommand, CastAbilityResult>
{
    public async Task<CastAbilityResult> Handle(
        CastAbilityCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var combatants = await LoadCombatants(command, cancellationToken);

        var resolved = SupportCastResolver.Resolve(
            combatants,
            new UseAbilityAction(command.TargetId, command.AbilityName),
            command.GameTime
        );
        if (resolved.ErrorMessage is not null)
            throw new InvalidOperationException(resolved.ErrorMessage);

        var state = combatEngine.ProcessPlayerTurn(combatants, resolved.Result!, command.GameTime);

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        await persistCreatureStates.Handle(
            new PersistCreatureStatesCommand
            {
                Updates = combatants.Select(c => c.ToCreatureStateUpdate()).ToArray(),
            },
            cancellationToken
        );
        await applyCombatUsage.Handle(
            new ApplyCombatUsageCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                State = state,
            },
            cancellationToken
        );

        transaction.Complete();

        var ability = ((ResolvedUseAbilityAction)resolved.Result!).Ability;
        var target = combatants.Single(c => c.CreatureId == command.TargetId);
        return new CastAbilityResult(
            ability.Name,
            ability.Description,
            target.Name,
            target.IsPlayer
        );
    }

    private async Task<IReadOnlyList<Combatant>> LoadCombatants(
        CastAbilityCommand command,
        CancellationToken cancellationToken
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );
        if (player is null)
            throw new InvalidOperationException("The player does not exist.");
        if (player.IsEngaged)
            throw new InvalidOperationException("You can't cast that while in a fight.");

        var playerCombatant = await combatantFactory.Create(player, true, cancellationToken);
        if (command.TargetId == command.PlayerId)
            return [playerCombatant];

        var target = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.TargetId },
            cancellationToken
        );
        if (target is null || target.LocationId != player.LocationId)
            throw new InvalidOperationException("That target is not here.");

        var targetCombatant = await combatantFactory.Create(target, false, cancellationToken);
        return [playerCombatant, targetCombatant];
    }
}
