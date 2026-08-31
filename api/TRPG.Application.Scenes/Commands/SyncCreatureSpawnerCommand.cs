using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.CreatureJobs.Commands;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Encounters.Commands;
using TRPG.Application.Factions.Queries;
using TRPG.Application.Inventory.Commands;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Scenes.Commands;

public class SyncCreatureSpawnerCommand
{
    public required Guid LocationId { get; init; }
    public required int PlayerLevel { get; init; }
    public required TimeSpan CurrentPlaytime { get; init; }
}

internal class SyncCreatureSpawnerCommandHandler(
    TrpgDbContext context,
    CreatureGenerator creatureGenerator,
    IQueryHandler<
        GetFactionsByCreatureTypeQuery,
        IReadOnlyDictionary<CreatureType, Faction>
    > getFactionsByCreatureType,
    ICommandHandler<AddCreaturesCommand> addCreatures,
    ICommandHandler<AddItemsCommand> addItems,
    ICommandHandler<AddCreatureSkillsCommand> addCreatureSkills,
    ICommandHandler<AddCreatureJobsCommand> addCreatureJobs,
    ICommandHandler<CreateEncounterGroupsCommand> createEncounterGroups
) : ICommandHandler<SyncCreatureSpawnerCommand>
{
    public async Task Handle(
        SyncCreatureSpawnerCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var spawner = await context.CreatureSpawners.FirstOrDefaultAsync(
            s => s.LocationId == command.LocationId,
            cancellationToken
        );
        if (spawner == null)
        {
            return;
        }

        var hasTriggered = RecurringScheduling.HasTriggered(
            spawner.TriggerHour,
            spawner.SpecificDay,
            spawner.LastSyncPlaytime,
            command.CurrentPlaytime
        );
        if (!hasTriggered)
        {
            return;
        }

        var currentPopulation = await context.Creatures.CountAsync(
            c => c.SpawnerId == spawner.Id && c.State != CreatureState.Dead,
            cancellationToken
        );

        var factionsByCreatureType = await getFactionsByCreatureType.Handle(
            new GetFactionsByCreatureTypeQuery { WorldId = spawner.WorldId },
            cancellationToken
        );

        var fillResult = CreatureSpawnFiller.Fill(
            creatureGenerator,
            spawner.ArchetypeCreatureTypes,
            currentPopulation,
            spawner.MaxPopulation,
            command.PlayerLevel,
            spawner.WorldId,
            spawner.LocationId,
            spawner.Id,
            factionsByCreatureType
        );

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        await addCreatures.Handle(
            new AddCreaturesCommand
            {
                Creatures = fillResult.Monsters.Select(m => m.Creature).ToArray(),
            },
            cancellationToken
        );
        await addItems.Handle(
            new AddItemsCommand { Items = fillResult.Monsters.SelectMany(m => m.Items).ToArray() },
            cancellationToken
        );
        await addCreatureSkills.Handle(
            new AddCreatureSkillsCommand
            {
                Skills = fillResult.Monsters.SelectMany(m => m.Skills).ToArray(),
            },
            cancellationToken
        );
        await addCreatureJobs.Handle(
            new AddCreatureJobsCommand { Jobs = fillResult.Jobs },
            cancellationToken
        );
        await createEncounterGroups.Handle(
            new CreateEncounterGroupsCommand
            {
                Groups = fillResult.EncounterGroups,
                Members = fillResult.EncounterGroupMembers,
            },
            cancellationToken
        );

        spawner.LastSyncPlaytime = command.CurrentPlaytime;
        await context.SaveChangesAsync(cancellationToken);

        transaction.Complete();
    }
}
