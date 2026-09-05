using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Combat;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.Crimes.Commands;
using TRPG.Application.Crimes.Events;
using TRPG.Application.Factions.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

internal class EndFightCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required CombatState State { get; init; }
}

internal class EndFightCommandHandler(
    IEncountersDbContext context,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    IQueryHandler<
        GetLiveHumanoidWitnessesAtLocationQuery,
        IReadOnlyCollection<LiveHumanoidWitness>
    > getLiveHumanoidWitnessesAtLocation,
    ICommandHandler<AddKillCrimesCommand> addKillCrimes,
    ICommandHandler<AddCrimeWitnessesCommand> addCrimeWitnesses,
    IQueryHandler<
        GetFactionIdsByCreatureIdsQuery,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>
    > getFactionIdsByCreatureIds,
    IGameClientEventSink gameEvents
) : ICommandHandler<EndFightCommand>
{
    public async Task Handle(EndFightCommand command, CancellationToken cancellationToken = default)
    {
        var state = command.State;

        using var transaction = new TransactionScope(
            TransactionScopeOption.Required,
            TransactionScopeAsyncFlowOption.Enabled
        );

        var playtime = await getPlaytime.Handle(
            new GetPlaytimeQuery { SessionId = command.SessionId },
            cancellationToken
        );

        var survivingCreatureIds = state
            .Combatants.Where(c => c.IsAlive)
            .Select(c => c.Id)
            .ToArray();

        await updateCreatures.Handle(
            new UpdateCreaturesCommand
            {
                CreatureIds = survivingCreatureIds,
                LastRegenPlaytime = playtime,
            },
            cancellationToken
        );

        var fight = await context
            .Encounters.OfType<FightEncounter>()
            .FirstOrDefaultAsync(
                item => item.WorldId == command.WorldId && item.Outcome == CombatOutcome.Ongoing,
                cancellationToken
            );

        var player = state.Combatants.FirstOrDefault(c => c.IsPlayer);
        if (player != null)
        {
            var killedCreatureIds = state
                .Combatants.Where(c => !c.IsPlayer && !c.IsAlive)
                .Select(c => c.Id)
                .ToArray();

            await RecordKillCrimes(
                fight,
                command.WorldId,
                player.Id,
                killedCreatureIds,
                state,
                cancellationToken
            );
        }

        if (fight != null)
        {
            fight.CompletedAt = DateTime.UtcNow;
            fight.State = EncounterState.Completed;
            fight.Outcome = state.Outcome;
            await context.SaveChangesAsync(cancellationToken);
        }

        transaction.Complete();
    }

    private async Task RecordKillCrimes(
        FightEncounter? fight,
        Guid worldId,
        Guid playerId,
        IReadOnlyCollection<Guid> killedCreatureIds,
        CombatState state,
        CancellationToken cancellationToken
    )
    {
        if (killedCreatureIds.Count == 0)
        {
            return;
        }

        if (fight == null)
        {
            return;
        }

        var liveWitnesses = await getLiveHumanoidWitnessesAtLocation.Handle(
            new GetLiveHumanoidWitnessesAtLocationQuery
            {
                WorldId = worldId,
                LocationId = fight.LocationId,
                ExcludeCreatureId = playerId,
            },
            cancellationToken
        );
        var witnesses = liveWitnesses.Select(witness => witness.Id).ToArray();

        var killedCombatants = state
            .Combatants.Where(combatant => killedCreatureIds.Contains(combatant.Id))
            .ToDictionary(combatant => combatant.Id);

        // Captured now because the victim's faction rows go with their corpse when it is cleaned up.
        var factionIdsByVictimId = await getFactionIdsByCreatureIds.Handle(
            new GetFactionIdsByCreatureIdsQuery { CreatureIds = killedCreatureIds },
            cancellationToken
        );

        var crimes = killedCreatureIds
            .Select(killedCreatureId => new KillCrime
            {
                WorldId = worldId,
                PlayerId = playerId,
                LocationId = fight.LocationId,
                VictimId = killedCreatureId,
                VictimName = killedCombatants[killedCreatureId].Name,
                VictimFactionIds = factionIdsByVictimId.TryGetValue(
                    killedCreatureId,
                    out var factionIds
                )
                    ? factionIds.ToList()
                    : [],
            })
            .ToArray();

        await addKillCrimes.Handle(new AddKillCrimesCommand { Crimes = crimes }, cancellationToken);
        await addCrimeWitnesses.Handle(
            new AddCrimeWitnessesCommand
            {
                WorldId = worldId,
                CrimeIds = crimes.Select(crime => crime.Id).ToArray(),
                WitnessCreatureIds = witnesses,
            },
            cancellationToken
        );

        if (witnesses.Length > 0)
        {
            await updateCreatures.Handle(
                new UpdateCreaturesCommand
                {
                    CreatureIds = witnesses,
                    State = CreatureState.Alerted,
                },
                cancellationToken
            );
            gameEvents.Enqueue(new CrimeWitnessedEvent(CrimeKind.Killing));
        }
    }
}
