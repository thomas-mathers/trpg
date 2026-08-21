using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.CreatureFormulas;
using TRPG.Application.Creatures;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Inventory;
using TRPG.Application.Inventory.Queries;
using TRPG.Application.Reputations.Queries;
using TRPG.Application.Trading;
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
    InventoryItemTransfer itemTransfer,
    ICommandHandler<AdjustCreatureSkillsCommand> adjustCreatureSkills,
    SkillCheckService skillCheckService,
    IQueryHandler<GetItemNamesByIdsQuery, IReadOnlyDictionary<Guid, string>> getItemNamesByIds,
    IQueryHandler<GetCityFactionForCreatureQuery, Guid?> getCityFactionForCreature,
    IDomainEventPublisher<ItemAcquiredEvent> itemAcquiredEvents,
    IOptionsMonitor<TheftOptions> theftOptions
) : ICommandHandler<AttemptTheftCommand, TheftAttemptResult>
{
    public async Task<TheftAttemptResult> Handle(
        AttemptTheftCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var source = await GetTheftSource(command, cancellationToken);
        if (source == null)
        {
            return new TheftAttemptResult(TheftAttemptOutcome.NotTheft);
        }

        if (command.Items.Count == 0)
        {
            return new TheftAttemptResult(TheftAttemptOutcome.NotTheft);
        }

        await itemTransfer.Validate(command.From, command.Items, cancellationToken);

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

        var witnesses = await GetLiveWitnesses(command, source.LocationId, cancellationToken);

        var crime = await CreateCrime(
            command,
            source,
            selections,
            itemNamesById,
            cancellationToken
        );

        var requiresTheftDetectionRoll = source.IsPickpocketing || witnesses.Length > 0;
        var options = theftOptions.CurrentValue;
        var isDetected =
            requiresTheftDetectionRoll
            && await skillCheckService.Roll(
                command.PlayerId,
                source.Skill,
                new SkillCheckCurve(
                    BaseChance: options.BaseDetectionChance,
                    ChanceChangePerSkillLevel: -options.DetectionChanceReductionPerSkillLevel,
                    MinimumChance: options.MinimumDetectionChance,
                    MaximumChance: options.MaximumDetectionChance
                ),
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
        var transferResults = await itemTransfer.Transfer(
            command.From,
            new ItemOwnerReference(command.PlayerId, OwnerType.Creature),
            command.Items,
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

        var confrontingCreature = GetConfrontingCreature(theft.Source, theft.Witnesses);
        if (confrontingCreature != null)
        {
            return await StartTheftEncounter(theft, confrontingCreature, cancellationToken);
        }

        return await CompleteDetectedTheftWithoutEncounter(theft, cancellationToken);
    }

    private async Task<TheftAttemptResult> StartTheftEncounter(
        DetectedTheftContext theft,
        ConfrontingCreature confrontingCreature,
        CancellationToken cancellationToken
    )
    {
        var transferResults = theft.Source.IsPickpocketing
            ? []
            : await itemTransfer.Transfer(
                theft.Command.From,
                new ItemOwnerReference(theft.Command.PlayerId, OwnerType.Creature),
                theft.Command.Items,
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
        var results = await itemTransfer.Transfer(
            theft.Command.From,
            new ItemOwnerReference(theft.Command.PlayerId, OwnerType.Creature),
            theft.Command.Items,
            cancellationToken
        );

        theft.Crime.Outcome = TheftCrimeOutcome.Taken;

        await context.SaveChangesAsync(cancellationToken);

        await PublishItemAcquiredEvents(theft.Command, results, cancellationToken);

        return new TheftAttemptResult(TheftAttemptOutcome.Completed);
    }

    private async Task<TheftSource?> GetTheftSource(
        AttemptTheftCommand command,
        CancellationToken cancellationToken
    )
    {
        if (command.From.Type == OwnerType.Creature)
        {
            var owner =
                await context.Creatures.FirstOrDefaultAsync(
                    creature =>
                        creature.Id == command.From.Id && creature.WorldId == command.WorldId,
                    cancellationToken
                ) ?? throw new EntityNotFoundException(nameof(Creature), command.From.Id);

            return owner.State == CreatureState.Dead
                ? null
                : new TheftSource(
                    Owner: owner,
                    LocationId: owner.LocationId,
                    Skill: Skill.Pickpocketing,
                    IsPickpocketing: true,
                    WorkstationOccupantId: null
                );
        }

        var prop =
            await context.Props.FirstOrDefaultAsync(
                candidate =>
                    candidate.Id == command.From.Id && candidate.WorldId == command.WorldId,
                cancellationToken
            ) ?? throw new EntityNotFoundException(nameof(Prop), command.From.Id);

        var workstationOccupantId = (command.From.Type, prop) switch
        {
            (OwnerType.Container, Container _) => null,
            (OwnerType.Workstation, Workstation workstation) => workstation.OccupantId,
            _ => throw new InvalidOperationException(
                $"Owner type {command.From.Type} does not match prop type {prop.GetType().Name}."
            ),
        };

        if (prop.OwnerCreatureId is not { } ownerId)
        {
            return null;
        }

        var sourceOwner =
            await context.Creatures.FirstOrDefaultAsync(
                creature => creature.Id == ownerId && creature.WorldId == command.WorldId,
                cancellationToken
            ) ?? throw new EntityNotFoundException(nameof(Creature), ownerId);

        return new TheftSource(
            Owner: sourceOwner,
            LocationId: prop.LocationId,
            Skill: Skill.Sneak,
            IsPickpocketing: false,
            WorkstationOccupantId: workstationOccupantId
        );
    }

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

    private async Task<TheftWitness[]> GetLiveWitnesses(
        AttemptTheftCommand command,
        Guid locationId,
        CancellationToken cancellationToken
    ) =>
        await context
            .Creatures.AsNoTracking()
            .Where(creature =>
                creature.WorldId == command.WorldId
                && creature.LocationId == locationId
                && creature.State != CreatureState.Dead
                && creature.Id != command.PlayerId
            )
            .Select(creature => new TheftWitness(creature.Id, creature.Name))
            .ToArrayAsync(cancellationToken);

    private static ConfrontingCreature? GetConfrontingCreature(
        TheftSource source,
        IReadOnlyCollection<TheftWitness> witnesses
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

    private sealed record TheftSource(
        Creature Owner,
        Guid LocationId,
        Skill Skill,
        bool IsPickpocketing,
        Guid? WorkstationOccupantId
    );

    private sealed record TheftWitness(Guid Id, string Name);

    private sealed record ConfrontingCreature(Guid Id, string Name);

    private sealed record DetectedTheftContext(
        AttemptTheftCommand Command,
        TheftSource Source,
        TheftCrime Crime,
        IReadOnlyCollection<TheftWitness> Witnesses,
        IReadOnlyCollection<ItemSelection> Selections,
        IReadOnlyDictionary<Guid, string> ItemNamesById
    );
}
