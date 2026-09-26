using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Crimes.Commands;
using TRPG.Application.Encounters.Mappers;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class ResolveTheftEncounterActionCommand : IEncounterResolutionCommand
{
    public required TheftEncounterAction Action { get; init; }
    public required Guid EncounterId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public GameInstant GameTime { get; init; } = GameClock.Epoch;
}

internal class ResolveTheftEncounterActionCommandHandler(
    IEncountersDbContext context,
    EncounterEngagementManager engagementManager,
    ICommandHandler<
        TransferInventoryItemsCommand,
        IReadOnlyCollection<InventoryItemTransferResult>
    > transferInventoryItems,
    ICommandHandler<SetTheftCrimeOutcomeCommand> setTheftCrimeOutcome,
    IQueryHandler<GetCreatureByIdQuery, Creature?> getCreatureById,
    EncounterFleeResolver encounterFleeResolver
)
    : EncounterResolutionCommandHandlerBase<
        TheftEncounter,
        ResolveTheftEncounterActionCommand,
        TheftEncounterResolutionFact
    >(context, engagementManager)
{
    protected override async Task<TheftEncounterResolutionFact> Resolve(
        ResolveTheftEncounterActionCommand command,
        TheftEncounter encounter,
        CancellationToken cancellationToken
    ) =>
        command.Action switch
        {
            ApologizeTheftEncounterAction => await ResolveApology(
                encounter,
                command,
                cancellationToken
            ),
            FleeTheftEncounterAction => await ResolveFlee(encounter, command, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };

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
            encounter.ToStolenFrom(),
            encounter.ItemNames.ToArray(),
            itemsReturned,
            ItemsHeldByPlayer: false,
            LeftTheScene: false
        );
    }

    private async Task<TheftEncounterResolutionFact> ResolveFlee(
        TheftEncounter encounter,
        ResolveTheftEncounterActionCommand command,
        CancellationToken cancellationToken
    )
    {
        var player =
            await getCreatureById.Handle(
                new GetCreatureByIdQuery { Id = command.PlayerId },
                cancellationToken
            ) ?? throw new EntityNotFoundException(nameof(Creature), command.PlayerId);

        // Settled before the move, because leaving is what resolves the crime.
        await setTheftCrimeOutcome.Handle(
            new SetTheftCrimeOutcomeCommand
            {
                CrimeId = encounter.TheftCrimeId,
                Outcome = TheftCrimeOutcome.Fled,
            },
            cancellationToken
        );

        var leftTheScene = await encounterFleeResolver.Resolve(
            encounter,
            player,
            command.GameTime,
            cancellationToken
        );

        return new TheftEncounterResolutionFact(
            encounter.Id,
            TheftEncounterResolutionOutcome.Fled,
            encounter.ConfrontingName,
            encounter.ToStolenFrom(),
            encounter.ItemNames.ToArray(),
            ItemsReturned: false,
            ItemsHeldByPlayer: encounter.ItemSelections.Count > 0,
            LeftTheScene: leftTheScene
        );
    }
}
