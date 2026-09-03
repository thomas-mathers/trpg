using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Crimes.Commands;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Data.ModuleContexts;
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
    IEncountersDbContext context,
    ICommandHandler<
        TransferInventoryItemsCommand,
        IReadOnlyCollection<InventoryItemTransferResult>
    > transferInventoryItems,
    ICommandHandler<SetTheftCrimeOutcomeCommand> setTheftCrimeOutcome,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures
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

        var resolution = command.Action switch
        {
            ApologizeTheftEncounterAction => await ResolveApology(
                encounter,
                command,
                cancellationToken
            ),
            FleeTheftEncounterAction => await ResolveFlee(encounter, command, cancellationToken),
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
            await transferInventoryItems.Handle(
                new TransferInventoryItemsCommand
                {
                    From = new ItemOwnerReference(command.PlayerId, OwnerType.Creature),
                    To = new ItemOwnerReference(sourceOwnerId, encounter.SourceOwnerType.Value),
                    Items = encounter
                        .ItemSelections.Select(item => new ItemSelection(
                            item.ItemId,
                            item.Quantity
                        ))
                        .ToArray(),
                },
                cancellationToken
            );
            itemsReturned = encounter.ItemSelections.Count > 0;
        }

        await setTheftCrimeOutcome.Handle(
            new SetTheftCrimeOutcomeCommand
            {
                CrimeId = encounter.TheftCrimeId,
                Outcome = TheftCrimeOutcome.Apologized,
            },
            cancellationToken
        );

        return new TheftEncounterResolutionFact(
            encounter.Id,
            TheftEncounterResolutionOutcome.Apologized,
            encounter.ConfrontingName,
            encounter.ItemNames.ToArray(),
            itemsReturned
        );
    }

    private async Task<TheftEncounterResolutionFact> ResolveFlee(
        TheftEncounter encounter,
        ResolveTheftEncounterActionCommand command,
        CancellationToken cancellationToken
    )
    {
        var player = await getCreatureById.Handle(
            new GetCreatureByIdQuery { Id = command.PlayerId },
            cancellationToken
        );
        if (player!.LocationId != encounter.LocationId)
        {
            await updateCreatures.Handle(
                new UpdateCreaturesCommand
                {
                    CreatureIds = [command.PlayerId],
                    LocationId = encounter.LocationId,
                },
                cancellationToken
            );
        }

        await setTheftCrimeOutcome.Handle(
            new SetTheftCrimeOutcomeCommand
            {
                CrimeId = encounter.TheftCrimeId,
                Outcome = TheftCrimeOutcome.Fled,
            },
            cancellationToken
        );

        return new TheftEncounterResolutionFact(
            encounter.Id,
            TheftEncounterResolutionOutcome.Fled,
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
}
