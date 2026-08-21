using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Exceptions;
using TRPG.Application.Common.Queries;
using TRPG.Application.Configuration;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Inventory;
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
    IQueryHandler<
        GetCreatureSkillsQuery,
        IReadOnlyCollection<CreatureSkillProgress>
    > getCreatureSkills,
    IDomainEventPublisher<ItemAcquiredEvent> itemAcquiredEvents,
    ITheftDetectionRoller detectionRoller,
    IOptionsMonitor<TheftOptions> theftOptions
) : ICommandHandler<AttemptTheftCommand, TheftAttemptResult>
{
    public async Task<TheftAttemptResult> Handle(
        AttemptTheftCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.Items.Count == 0)
        {
            return new TheftAttemptResult(TheftAttemptOutcome.NotTheft);
        }

        var source = await GetTheftSource(command, cancellationToken);
        if (source == null)
        {
            return new TheftAttemptResult(TheftAttemptOutcome.NotTheft);
        }

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        await itemTransfer.Validate(command.From, command.Items, cancellationToken);

        var selections = ToSelections(command.Items);
        var itemNamesById = await GetItemNames(selections, cancellationToken);
        var witnesses = await GetLiveWitnesses(command, source.LocationId, cancellationToken);
        var crime = await CreateCrime(
            command,
            source,
            selections,
            itemNamesById,
            cancellationToken
        );
        var requiresCheck = source.IsPickpocketing || witnesses.Length > 0;

        if (requiresCheck && await IsDetected(command, source.Skill, cancellationToken))
        {
            var result = await ResolveDetectedTheft(
                command,
                source,
                crime,
                witnesses,
                selections,
                itemNamesById,
                cancellationToken
            );
            transaction.Complete();
            return result;
        }

        var transferResults = await TransferToPlayer(command, cancellationToken);
        crime.Outcome = TheftCrimeOutcome.Taken;

        if (requiresCheck)
        {
            await AwardSkillExperience(command, source.Skill, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        await PublishItemAcquiredEvents(command, transferResults, cancellationToken);

        transaction.Complete();
        return new TheftAttemptResult(TheftAttemptOutcome.Completed);
    }

    private async Task<TheftAttemptResult> ResolveDetectedTheft(
        AttemptTheftCommand command,
        TheftSource source,
        TheftCrime crime,
        IReadOnlyCollection<Guid> witnesses,
        IReadOnlyCollection<ItemSelection> selections,
        IReadOnlyDictionary<Guid, string> itemNamesById,
        CancellationToken cancellationToken
    )
    {
        context.CrimeWitnesses.AddRange(
            witnesses.Select(witnessId => new CrimeWitness
            {
                WorldId = command.WorldId,
                CrimeId = crime.Id,
                CreatureId = witnessId,
            })
        );

        var ownerIsPresent =
            source.Owner.State != CreatureState.Dead
            && source.Owner.LocationId == source.LocationId;
        if (source.IsPickpocketing || ownerIsPresent)
        {
            IReadOnlyCollection<InventoryItemTransferResult> transferResults =
                source.IsPickpocketing ? [] : await TransferToPlayer(command, cancellationToken);
            crime.Outcome = source.IsPickpocketing ? null : TheftCrimeOutcome.Taken;
            var encounter = CreateEncounter(
                command,
                source,
                crime,
                witnesses,
                selections,
                itemNamesById,
                transferResults
            );
            context.Encounters.Add(encounter);
            await context.SaveChangesAsync(cancellationToken);
            await PublishItemAcquiredEvents(command, transferResults, cancellationToken);
            return new TheftAttemptResult(TheftAttemptOutcome.EncounterPending, encounter.Id);
        }

        var results = await TransferToPlayer(command, cancellationToken);
        crime.Outcome = TheftCrimeOutcome.Taken;
        await context.SaveChangesAsync(cancellationToken);
        await PublishItemAcquiredEvents(command, results, cancellationToken);
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
                : new TheftSource(owner, owner.LocationId, Skill.Pickpocketing, true);
        }

        var prop = await context.Props.FirstOrDefaultAsync(
            candidate => candidate.Id == command.From.Id && candidate.WorldId == command.WorldId,
            cancellationToken
        );
        if (prop == null)
        {
            throw new EntityNotFoundException(nameof(Prop), command.From.Id);
        }

        var ownerId = prop switch
        {
            Container { OwnerCreatureId: { } containerOwnerId }
                when command.From.Type == OwnerType.Container => containerOwnerId,
            Workstation { AssignedCreatureId: { } workstationOwnerId }
                when command.From.Type == OwnerType.Workstation => workstationOwnerId,
            _ => (Guid?)null,
        };
        if (ownerId == null)
        {
            return null;
        }

        var sourceOwner = await context.Creatures.FirstOrDefaultAsync(
            creature => creature.Id == ownerId && creature.WorldId == command.WorldId,
            cancellationToken
        );
        return sourceOwner == null
            ? null
            : new TheftSource(sourceOwner, prop.LocationId, Skill.Sneak, false);
    }

    private async Task<bool> IsDetected(
        AttemptTheftCommand command,
        Skill skill,
        CancellationToken cancellationToken
    )
    {
        var playerSkills = await getCreatureSkills.Handle(
            new GetCreatureSkillsQuery { CreatureId = command.PlayerId },
            cancellationToken
        );
        var skillLevel = playerSkills.SingleOrDefault(item => item.Skill == skill)?.Level ?? 0;
        var chance = TheftDetectionCalculator.CalculateChance(
            skillLevel,
            theftOptions.CurrentValue
        );
        return detectionRoller.IsDetected(chance);
    }

    private async Task<TheftCrime> CreateCrime(
        AttemptTheftCommand command,
        TheftSource source,
        IReadOnlyCollection<ItemSelection> selections,
        IReadOnlyDictionary<Guid, string> itemNamesById,
        CancellationToken cancellationToken
    )
    {
        var ownerFactionId = await (
            from member in context.FactionMembers.AsNoTracking()
            join faction in context.Factions.AsNoTracking() on member.FactionId equals faction.Id
            where member.CreatureId == source.Owner.Id && faction.IsCityFaction
            select (Guid?)faction.Id
        ).FirstOrDefaultAsync(cancellationToken);
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

    private async Task<Guid[]> GetLiveWitnesses(
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
            .Select(creature => creature.Id)
            .ToArrayAsync(cancellationToken);

    private async Task<IReadOnlyDictionary<Guid, string>> GetItemNames(
        IReadOnlyCollection<ItemSelection> selections,
        CancellationToken cancellationToken
    ) =>
        await context
            .Items.AsNoTracking()
            .Where(item =>
                selections.Select(selection => selection.ItemId).AsEnumerable().Contains(item.Id)
            )
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);

    private async Task<IReadOnlyCollection<InventoryItemTransferResult>> TransferToPlayer(
        AttemptTheftCommand command,
        CancellationToken cancellationToken
    ) =>
        await itemTransfer.Transfer(
            command.From,
            new ItemOwnerReference(command.PlayerId, OwnerType.Creature),
            command.Items,
            cancellationToken
        );

    private async Task AwardSkillExperience(
        AttemptTheftCommand command,
        Skill skill,
        CancellationToken cancellationToken
    ) =>
        await adjustCreatureSkills.Handle(
            new AdjustCreatureSkillsCommand
            {
                WorldId = command.WorldId,
                CreatureId = command.PlayerId,
                UsageCounts = new Dictionary<Skill, int> { [skill] = 1 },
            },
            cancellationToken
        );

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

    private static TheftEncounter CreateEncounter(
        AttemptTheftCommand command,
        TheftSource source,
        TheftCrime crime,
        IReadOnlyCollection<Guid> witnesses,
        IReadOnlyCollection<ItemSelection> selections,
        IReadOnlyDictionary<Guid, string> itemNamesById,
        IReadOnlyCollection<InventoryItemTransferResult> transferResults
    ) =>
        new()
        {
            TheftCrimeId = crime.Id,
            WorldId = command.WorldId,
            PlayerId = command.PlayerId,
            LocationId = source.LocationId,
            OwnerCreatureId = source.Owner.Id,
            OwnerName = source.Owner.Name,
            SourceOwnerId = command.From.Id,
            SourceOwnerType = command.From.Type,
            ItemIds = selections.Select(item => item.ItemId).ToList(),
            ItemNames = selections.Select(item => itemNamesById[item.ItemId]).ToList(),
            ItemSelections = transferResults
                .Select(result => new TheftEncounterItem(result.DestinationItemId, result.Quantity))
                .ToList(),
            WitnessCreatureIds = witnesses.ToList(),
        };

    private static ItemSelection[] ToSelections(IReadOnlyList<ItemSelection> items) =>
        items
            .GroupBy(item => item.ItemId)
            .Select(group => new ItemSelection(group.Key, group.Sum(item => item.Quantity)))
            .ToArray();

    private sealed record TheftSource(
        Creature Owner,
        Guid LocationId,
        Skill Skill,
        bool IsPickpocketing
    );
}
