using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Factions.Queries;
using TRPG.Application.WorldGeneration.Generators;
using TRPG.Data.ModuleContexts;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.LocationSimulation.Commands;

public class SyncCreatureSpawnerCommand
{
    public required Guid LocationId { get; init; }
    public required int PlayerLevel { get; init; }
    public required GameInstant CurrentGameTime { get; init; }
}

public record SyncCreatureSpawnerResult(IReadOnlyCollection<Guid> SpawnedEncounterGroupIds)
{
    public static readonly SyncCreatureSpawnerResult None = new([]);
}

internal class SyncCreatureSpawnerCommandHandler(
    ILocationSimulationDbContext context,
    CreatureGenerator creatureGenerator,
    IQueryHandler<
        GetFactionsByCreatureTypeQuery,
        IReadOnlyDictionary<CreatureType, Faction>
    > getFactionsByCreatureType,
    ICommandHandler<AddCreatureSpawnResultCommand> addCreatureSpawnResult,
    IQueryHandler<GetLivingCreatureCountBySpawnerIdQuery, int> getLivingCreatureCountBySpawnerId
) : ICommandHandler<SyncCreatureSpawnerCommand, SyncCreatureSpawnerResult>
{
    public async Task<SyncCreatureSpawnerResult> Handle(
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
            return SyncCreatureSpawnerResult.None;
        }

        var hasTriggered = RecurringScheduling.HasTriggered(
            spawner.Schedule,
            spawner.LastSyncGameTime,
            command.CurrentGameTime
        );
        if (!hasTriggered)
        {
            return SyncCreatureSpawnerResult.None;
        }

        var currentPopulation = await getLivingCreatureCountBySpawnerId.Handle(
            new GetLivingCreatureCountBySpawnerIdQuery { SpawnerId = spawner.Id },
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
            factionsByCreatureType,
            spawner.FactionId
        );

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        await addCreatureSpawnResult.Handle(
            new AddCreatureSpawnResultCommand
            {
                Monsters = fillResult.Monsters,
                Jobs = fillResult.Jobs,
                EncounterGroups = fillResult.EncounterGroups,
                EncounterGroupMembers = fillResult.EncounterGroupMembers,
                FactionMembers = fillResult.FactionMembers,
            },
            cancellationToken
        );

        spawner.LastSyncGameTime = command.CurrentGameTime;
        await context.SaveChangesAsync(cancellationToken);

        transaction.Complete();

        return new SyncCreatureSpawnerResult(
            fillResult.EncounterGroups.Select(group => group.Id).ToArray()
        );
    }
}
