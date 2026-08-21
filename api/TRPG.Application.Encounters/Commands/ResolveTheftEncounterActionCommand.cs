using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Combat.Commands;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Inventory;
using TRPG.Application.Trading;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class ResolveTheftEncounterActionCommand
{
    public required TheftEncounterAction Action { get; init; }
    public required Guid EncounterId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
}

internal class ResolveTheftEncounterActionCommandHandler(
    TrpgDbContext context,
    InventoryItemTransfer itemTransfer,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    ICommandHandler<StartFightCommand> startFight
) : ICommandHandler<ResolveTheftEncounterActionCommand, TheftEncounterResolutionFact>
{
    public async Task<TheftEncounterResolutionFact> Handle(
        ResolveTheftEncounterActionCommand command,
        CancellationToken cancellationToken = default
    )
    {
        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var encounter = await GetEncounter(command, cancellationToken);
        var crime = await GetCrime(encounter, cancellationToken);

        var resolution = command.Action switch
        {
            ApologizeTheftEncounterAction => await ResolveApology(
                encounter,
                crime,
                command,
                cancellationToken
            ),
            FightTheftEncounterAction => await ResolveFight(
                encounter,
                crime,
                command,
                cancellationToken
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };

        encounter.State = EncounterState.Completed;
        encounter.CompletedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        transaction.Complete();

        return resolution;
    }

    private async Task<TheftEncounterResolutionFact> ResolveApology(
        TheftEncounter encounter,
        TheftCrime crime,
        ResolveTheftEncounterActionCommand command,
        CancellationToken cancellationToken
    )
    {
        var itemsReturned = false;
        if (
            encounter.SourceOwnerId is { } sourceOwnerId
            && encounter.SourceOwnerType is OwnerType.Container or OwnerType.Workstation
        )
        {
            await itemTransfer.Transfer(
                new ItemOwnerReference(command.PlayerId, OwnerType.Creature),
                new ItemOwnerReference(sourceOwnerId, encounter.SourceOwnerType.Value),
                encounter
                    .ItemSelections.Select(item => new ItemSelection(item.ItemId, item.Quantity))
                    .ToArray(),
                cancellationToken
            );
            itemsReturned = encounter.ItemSelections.Count > 0;
        }

        crime.Outcome = TheftCrimeOutcome.Apologized;

        return new TheftEncounterResolutionFact(
            encounter.Id,
            TheftEncounterResolutionOutcome.Apologized,
            encounter.ConfrontingName,
            encounter.ItemNames.ToArray(),
            itemsReturned
        );
    }

    private async Task<TheftEncounterResolutionFact> ResolveFight(
        TheftEncounter encounter,
        TheftCrime crime,
        ResolveTheftEncounterActionCommand command,
        CancellationToken cancellationToken
    )
    {
        crime.Outcome = TheftCrimeOutcome.Resisted;

        await updateCreatures.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = [encounter.ConfrontingCreatureId],
                State = CreatureState.Alerted,
            },
            cancellationToken
        );
        await startFight.Handle(
            new StartFightCommand
            {
                SessionId = command.SessionId,
                WorldId = command.WorldId,
                PlayerId = command.PlayerId,
                EnemyCreatureIds = [encounter.ConfrontingCreatureId],
            },
            cancellationToken
        );

        return new TheftEncounterResolutionFact(
            encounter.Id,
            TheftEncounterResolutionOutcome.Fought,
            encounter.ConfrontingName,
            encounter.ItemNames.ToArray(),
            false
        );
    }

    private async Task<TheftEncounter> GetEncounter(
        ResolveTheftEncounterActionCommand command,
        CancellationToken cancellationToken
    )
    {
        var encounter = await context
            .Encounters.OfType<TheftEncounter>()
            .FirstOrDefaultAsync(
                item =>
                    item.Id == command.EncounterId
                    && item.WorldId == command.WorldId
                    && item.PlayerId == command.PlayerId,
                cancellationToken
            );
        if (encounter == null)
        {
            throw new EntityNotFoundException(nameof(TheftEncounter), command.EncounterId);
        }
        if (encounter.State != EncounterState.Active)
        {
            throw new InvalidOperationException("The theft encounter has already been resolved.");
        }

        return encounter;
    }

    private async Task<TheftCrime> GetCrime(
        TheftEncounter encounter,
        CancellationToken cancellationToken
    ) =>
        await context
            .Crimes.OfType<TheftCrime>()
            .FirstOrDefaultAsync(
                item =>
                    item.Id == encounter.TheftCrimeId
                    && item.WorldId == encounter.WorldId
                    && item.PlayerId == encounter.PlayerId,
                cancellationToken
            )
        ?? throw new EntityNotFoundException(nameof(TheftCrime), encounter.TheftCrimeId);
}
