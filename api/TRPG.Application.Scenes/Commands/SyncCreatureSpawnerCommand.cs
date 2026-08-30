using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Reputations.Queries;
using TRPG.Application.Worlds.Generators;
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
    > getFactionsByCreatureType
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

        context.Creatures.AddRange(fillResult.Monsters.Select(m => m.Creature));
        context.Items.AddRange(fillResult.Monsters.SelectMany(m => m.Items));
        context.CreatureSkills.AddRange(fillResult.Monsters.SelectMany(m => m.Skills));
        context.CreatureJobs.AddRange(fillResult.Jobs);
        context.EncounterGroups.AddRange(fillResult.EncounterGroups);
        context.EncounterGroupMembers.AddRange(fillResult.EncounterGroupMembers);

        spawner.LastSyncPlaytime = command.CurrentPlaytime;

        await context.SaveChangesAsync(cancellationToken);
    }
}
