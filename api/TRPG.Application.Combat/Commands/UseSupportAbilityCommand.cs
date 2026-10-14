using System.Transactions;
using TRPG.Application.Combat.Mappers;
using TRPG.Application.Common.Commands;
using TRPG.Application.Creatures.Commands;
using TRPG.Domain;

namespace TRPG.Application.Combat.Commands;

public class UseSupportAbilityCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid TargetId { get; init; }
    public required string AbilityName { get; init; }
    public required IReadOnlyList<Combatant> Combatants { get; init; }
    public required GameInstant GameTime { get; init; }
}

internal class UseSupportAbilityCommandHandler(
    CombatEngine combatEngine,
    ICommandHandler<PersistCreatureStatesCommand> persistCreatureStates,
    ICommandHandler<ApplyCombatUsageCommand> applyCombatUsage
) : ICommandHandler<UseSupportAbilityCommand, AbilityUseResult>
{
    public async Task<AbilityUseResult> Handle(
        UseSupportAbilityCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var use = combatEngine.UseAbility(
            command.Combatants,
            command.PlayerId,
            command.TargetId,
            command.AbilityName,
            command.GameTime
        );
        if (use.State is null)
        {
            return use;
        }

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        await persistCreatureStates.Handle(
            new PersistCreatureStatesCommand
            {
                Updates = command
                    .Combatants.Select(combatant => combatant.ToCreatureStateUpdate())
                    .ToArray(),
            },
            cancellationToken
        );
        await applyCombatUsage.Handle(
            new ApplyCombatUsageCommand
            {
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                GameTime = command.GameTime,
                State = use.State,
            },
            cancellationToken
        );

        transaction.Complete();

        return use;
    }
}
