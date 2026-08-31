using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Encounters;
using TRPG.Application.Factions.Queries;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Reputations.Events;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public enum TheftAttemptOutcome
{
    NotTheft,
    Completed,
    EncounterPending,
}

public record TheftAttemptResult(TheftAttemptOutcome Outcome, Guid? EncounterId = null);

public class AttemptTheftCommand
{
    public required ItemOwnerReference From { get; init; }

    public required IReadOnlyList<ItemSelection> Items { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
}

internal class AttemptTheftCommandHandler(
    TrpgDbContext context,
    TheftSourceResolver theftSourceResolver,
    ICommandHandler<
        TransferInventoryItemsCommand,
        IReadOnlyCollection<InventoryItemTransferResult>
    > transferInventoryItems,
    ICommandHandler<AdjustCreatureSkillsCommand> adjustCreatureSkills,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    SkillCheckService skillCheckService,
    IQueryHandler<GetItemNamesByIdsQuery, IReadOnlyDictionary<Guid, string>> getItemNamesByIds,
    IQueryHandler<GetCityFactionForCreatureQuery, Guid?> getCityFactionForCreature,
    IQueryHandler<
        ValidateTransferItemsQuery,
        IReadOnlyCollection<TransferItem>
    > validateTransferItems,
    IQueryHandler<
        GetLiveHumanoidWitnessesAtLocationQuery,
        IReadOnlyCollection<LiveHumanoidWitness>
    > getLiveHumanoidWitnessesAtLocation,
    IQueryHandler<GetEquippedItemCountQuery, int> getEquippedItemCount,
    IDomainEventPublisher<ItemAcquiredEvent> itemAcquiredEvents,
    IGameClientEventSink gameEvents,
    IOptionsMonitor<TheftOptions> theftOptions
) : ICommandHandler<AttemptTheftCommand, TheftAttemptResult>
{
    public async Task<TheftAttemptResult> Handle(
        AttemptTheftCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var source = await theftSourceResolver.Resolve(
            command.From,
            command.WorldId,
            cancellationToken
        );
        if (source == null)
        {
            return new TheftAttemptResult(TheftAttemptOutcome.NotTheft);
        }

        if (command.Items.Count == 0)
        {
            return new TheftAttemptResult(TheftAttemptOutcome.NotTheft);
        }

        await ValidateTransferableItems(command.From, command.Items, cancellationToken);

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var selections = command
            .Items.GroupBy(item => item.ItemId)
            .Select(group => new ItemSelection(group.Key, group.Sum(item => item.Quantity)))
            .ToArray();

        var itemNamesById = await getItemNamesByIds.Handle(
            new GetItemNamesByIdsQuery
            {
                WorldId = command.WorldId,
                ItemIds = selections.Select(item => item.ItemId).ToArray(),
            },
            cancellationToken
        );

        var witnesses = await getLiveHumanoidWitnessesAtLocation.Handle(
            new GetLiveHumanoidWitnessesAtLocationQuery
            {
                WorldId = command.WorldId,
                LocationId = source.LocationId,
                ExcludeCreatureId = command.PlayerId,
            },
            cancellationToken
        );

        var crime = await CreateCrime(
            command,
            source,
            selections,
            itemNamesById,
            cancellationToken
        );

        var requiresTheftDetectionRoll = source.IsPickpocketing || witnesses.Count > 0;

        var options = theftOptions.CurrentValue;
        var totalQuantity = selections.Sum(selection => selection.Quantity);
        var equippedItemCount = await getEquippedItemCount.Handle(
            new GetEquippedItemCountQuery
            {
                WorldId = command.WorldId,
                ItemIds = selections.Select(selection => selection.ItemId).ToArray(),
            },
            cancellationToken
        );
        var curve = TheftDetectionChanceCalculator.BuildCurve(
            options,
            totalQuantity,
            equippedItemCount
        );

        var isDetected =
            requiresTheftDetectionRoll
            && await skillCheckService.Roll(
                command.PlayerId,
                source.Skill,
                curve,
                cancellationToken
            );

        if (isDetected)
        {
            var result = await ResolveDetectedTheft(
                new DetectedTheftContext(
                    Command: command,
                    Source: source,
                    Crime: crime,
                    Witnesses: witnesses,
                    Selections: selections,
                    ItemNamesById: itemNamesById
                ),
                cancellationToken
            );
            transaction.Complete();
            return result;
        }

        var undetectedTheft = await ResolveUndetectedTheft(
            command,
            source,
            crime,
            requiresTheftDetectionRoll,
            cancellationToken
        );

        transaction.Complete();

        return undetectedTheft;
    }

    private async Task<TheftAttemptResult> ResolveUndetectedTheft(
        AttemptTheftCommand command,
        TheftSource source,
        TheftCrime crime,
        bool requiresTheftDetectionRoll,
        CancellationToken cancellationToken
    )
    {
        var transferResults = await transferInventoryItems.Handle(
            new TransferInventoryItemsCommand
            {
                From = command.From,
                To = new ItemOwnerReference(command.PlayerId, OwnerType.Creature),
                Items = command.Items,
            },
            cancellationToken
        );

        crime.Outcome = TheftCrimeOutcome.Taken;

        if (requiresTheftDetectionRoll)
        {
            await adjustCreatureSkills.Handle(
                new AdjustCreatureSkillsCommand
                {
                    WorldId = command.WorldId,
                    CreatureId = command.PlayerId,
                    UsageCounts = new Dictionary<Skill, int> { [source.Skill] = 1 },
                },
                cancellationToken
            );
        }

        await context.SaveChangesAsync(cancellationToken);

        await PublishItemAcquiredEvents(command, transferResults, cancellationToken);

        return new TheftAttemptResult(TheftAttemptOutcome.Completed);
    }

    private async Task<TheftAttemptResult> ResolveDetectedTheft(
        DetectedTheftContext theft,
        CancellationToken cancellationToken
    )
    {
        context.CrimeWitnesses.AddRange(
            theft.Witnesses.Select(witness => new CrimeWitness
            {
                WorldId = theft.Command.WorldId,
                CrimeId = theft.Crime.Id,
                CreatureId = witness.Id,
            })
        );

        if (theft.Witnesses.Count > 0)
        {
            await updateCreatures.Handle(
                new UpdateCreaturesCommand
                {
                    CreatureIds = theft.Witnesses.Select(witness => witness.Id).ToArray(),
                    State = CreatureState.Alerted,
                },
                cancellationToken
            );
        }

        var confrontingCreature = GetConfrontingCreature(theft.Source, theft.Witnesses);
        var result =
            confrontingCreature != null
                ? await StartTheftEncounter(theft, confrontingCreature, cancellationToken)
                : await CompleteDetectedTheftWithoutEncounter(theft, cancellationToken);

        gameEvents.Enqueue(new CrimeWitnessedEvent(CrimeKind.Theft));

        return result;
    }

    private async Task<TheftAttemptResult> StartTheftEncounter(
        DetectedTheftContext theft,
        ConfrontingCreature confrontingCreature,
        CancellationToken cancellationToken
    )
    {
        var transferResults = theft.Source.IsPickpocketing
            ? []
            : await transferInventoryItems.Handle(
                new TransferInventoryItemsCommand
                {
                    From = theft.Command.From,
                    To = new ItemOwnerReference(theft.Command.PlayerId, OwnerType.Creature),
                    Items = theft.Command.Items,
                },
                cancellationToken
            );

        theft.Crime.Outcome = theft.Source.IsPickpocketing ? null : TheftCrimeOutcome.Taken;

        var encounter = new TheftEncounter
        {
            TheftCrimeId = theft.Crime.Id,
            WorldId = theft.Command.WorldId,
            PlayerId = theft.Command.PlayerId,
            LocationId = theft.Source.LocationId,
            ConfrontingCreatureId = confrontingCreature.Id,
            ConfrontingName = confrontingCreature.Name,
            SourceOwnerId = theft.Command.From.Id,
            SourceOwnerType = theft.Command.From.Type,
            ItemIds = theft.Selections.Select(item => item.ItemId).ToList(),
            ItemNames = theft.Selections.Select(item => theft.ItemNamesById[item.ItemId]).ToList(),
            ItemSelections = transferResults
                .Select(result => new TheftEncounterItem(result.DestinationItemId, result.Quantity))
                .ToList(),
            WitnessCreatureIds = theft.Witnesses.Select(witness => witness.Id).ToList(),
        };

        context.Encounters.Add(encounter);

        await context.SaveChangesAsync(cancellationToken);

        await PublishItemAcquiredEvents(theft.Command, transferResults, cancellationToken);

        return new TheftAttemptResult(TheftAttemptOutcome.EncounterPending, encounter.Id);
    }

    private async Task<TheftAttemptResult> CompleteDetectedTheftWithoutEncounter(
        DetectedTheftContext theft,
        CancellationToken cancellationToken
    )
    {
        var results = await transferInventoryItems.Handle(
            new TransferInventoryItemsCommand
            {
                From = theft.Command.From,
                To = new ItemOwnerReference(theft.Command.PlayerId, OwnerType.Creature),
                Items = theft.Command.Items,
            },
            cancellationToken
        );

        theft.Crime.Outcome = TheftCrimeOutcome.Taken;

        await context.SaveChangesAsync(cancellationToken);

        await PublishItemAcquiredEvents(theft.Command, results, cancellationToken);

        return new TheftAttemptResult(TheftAttemptOutcome.Completed);
    }

    private async Task ValidateTransferableItems(
        ItemOwnerReference from,
        IReadOnlyList<ItemSelection> selections,
        CancellationToken cancellationToken
    ) =>
        _ = await validateTransferItems.Handle(
            new ValidateTransferItemsQuery { From = from, Selections = selections },
            cancellationToken
        );

    private async Task<TheftCrime> CreateCrime(
        AttemptTheftCommand command,
        TheftSource source,
        IReadOnlyCollection<ItemSelection> selections,
        IReadOnlyDictionary<Guid, string> itemNamesById,
        CancellationToken cancellationToken
    )
    {
        var ownerFactionId = await getCityFactionForCreature.Handle(
            new GetCityFactionForCreatureQuery { CreatureId = source.Owner.Id },
            cancellationToken
        );

        var crime = new TheftCrime
        {
            WorldId = command.WorldId,
            PlayerId = command.PlayerId,
            LocationId = source.LocationId,
            OwnerCreatureId = source.Owner.Id,
            OwnerFactionId = ownerFactionId,
            OwnerName = source.Owner.Name,
            SourceOwnerId = command.From.Id,
            SourceOwnerType = command.From.Type,
            Items = selections
                .Select(selection => new TheftCrimeItem(
                    itemNamesById[selection.ItemId],
                    selection.Quantity
                ))
                .ToList(),
        };
        context.Crimes.Add(crime);

        return crime;
    }

    private static ConfrontingCreature? GetConfrontingCreature(
        TheftSource source,
        IReadOnlyCollection<LiveHumanoidWitness> witnesses
    )
    {
        var ownerIsPresent =
            source.Owner.State != CreatureState.Dead
            && source.Owner.LocationId == source.LocationId;
        if (ownerIsPresent)
        {
            return new ConfrontingCreature(source.Owner.Id, source.Owner.Name);
        }

        if (source.WorkstationOccupantId is not { } workstationOccupantId)
        {
            return null;
        }

        var workstationOccupant = witnesses.SingleOrDefault(witness =>
            witness.Id == workstationOccupantId
        );
        return workstationOccupant == null
            ? null
            : new ConfrontingCreature(workstationOccupant.Id, workstationOccupant.Name);
    }

    private async Task PublishItemAcquiredEvents(
        AttemptTheftCommand command,
        IReadOnlyCollection<InventoryItemTransferResult> transferResults,
        CancellationToken cancellationToken
    )
    {
        foreach (var result in transferResults)
        {
            await itemAcquiredEvents.Publish(
                new ItemAcquiredEvent(command.PlayerId, command.WorldId, result.SourceItemId),
                cancellationToken
            );
        }
    }

    private sealed record ConfrontingCreature(Guid Id, string Name);

    private sealed record DetectedTheftContext(
        AttemptTheftCommand Command,
        TheftSource Source,
        TheftCrime Crime,
        IReadOnlyCollection<LiveHumanoidWitness> Witnesses,
        IReadOnlyCollection<ItemSelection> Selections,
        IReadOnlyDictionary<Guid, string> ItemNamesById
    );
}
