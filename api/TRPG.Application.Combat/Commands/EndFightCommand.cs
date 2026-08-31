using System.Transactions;
using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.Creatures.Queries;
using TRPG.Application.GameSessions.Queries;
using TRPG.Application.Reputations.Events;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Combat.Commands;

internal class EndFightCommand
{
    public required Guid SessionId { get; init; }
    public required Guid WorldId { get; init; }
    public required CombatState State { get; init; }
}

internal class EndFightCommandHandler(
    TrpgDbContext context,
    ICommandHandler<UpdateCreaturesCommand> updateCreatures,
    IQueryHandler<GetPlaytimeQuery, TimeSpan> getPlaytime,
    IQueryHandler<
        GetLiveHumanoidWitnessesAtLocationQuery,
        IReadOnlyCollection<LiveHumanoidWitness>
    > getLiveHumanoidWitnessesAtLocation,
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

        foreach (var killedCreatureId in killedCreatureIds)
        {
            var crime = new KillCrime
            {
                WorldId = worldId,
                PlayerId = playerId,
                LocationId = fight.LocationId,
                VictimId = killedCreatureId,
                VictimName = killedCombatants[killedCreatureId].Name,
            };
            context.Crimes.Add(crime);
            context.CrimeWitnesses.AddRange(
                witnesses.Select(witnessId => new CrimeWitness
                {
                    WorldId = worldId,
                    CrimeId = crime.Id,
                    CreatureId = witnessId,
                })
            );
        }

        await context.SaveChangesAsync(cancellationToken);

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
