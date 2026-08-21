using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Reputations.Events;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Reputations.Commands;

public class ResolveKillCrimesCommand
{
    public required Guid LocationId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
}

internal class ResolveKillCrimesCommandHandler(
    TrpgDbContext context,
    ICommandHandler<ApplyReputationPenaltyForKillsCommand> applyReputationPenaltyForKills,
    IGameClientEventSink gameEvents
) : ICommandHandler<ResolveKillCrimesCommand>
{
    public async Task Handle(
        ResolveKillCrimesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var crimes = await context
            .Crimes.OfType<KillCrime>()
            .Where(crime =>
                crime.WorldId == command.WorldId
                && crime.PlayerId == command.PlayerId
                && crime.LocationId == command.LocationId
                && crime.Resolution == CrimeResolution.Pending
            )
            .ToArrayAsync(cancellationToken);
        if (crimes.Length == 0)
        {
            return;
        }

        var crimeIds = crimes.Select(crime => crime.Id).ToArray();
        var witnesses = await context
            .CrimeWitnesses.Where(witness =>
                crimeIds.AsEnumerable().Contains(witness.CrimeId)
                && witness.Resolution == CrimeWitnessResolution.Pending
            )
            .ToArrayAsync(cancellationToken);
        var witnessCreatureIds = witnesses
            .Select(witness => witness.CreatureId)
            .Distinct()
            .ToArray();
        var liveWitnessIds = await context
            .Creatures.AsNoTracking()
            .Where(creature =>
                creature.WorldId == command.WorldId
                && witnessCreatureIds.AsEnumerable().Contains(creature.Id)
                && creature.State != CreatureState.Dead
            )
            .Select(creature => creature.Id)
            .ToArrayAsync(cancellationToken);

        foreach (var witness in witnesses)
        {
            witness.Resolution = liveWitnessIds.Contains(witness.CreatureId)
                ? CrimeWitnessResolution.Reported
                : CrimeWitnessResolution.Dead;
            witness.ResolvedAt = DateTime.UtcNow;
        }

        var reportedCrimes = crimes
            .Where(crime =>
                witnesses.Any(witness =>
                    witness.CrimeId == crime.Id
                    && witness.Resolution == CrimeWitnessResolution.Reported
                )
            )
            .ToArray();

        if (reportedCrimes.Length > 0)
        {
            await applyReputationPenaltyForKills.Handle(
                new ApplyReputationPenaltyForKillsCommand
                {
                    KillerId = command.PlayerId,
                    KilledCreatureIds = reportedCrimes.Select(crime => crime.VictimId).ToArray(),
                },
                cancellationToken
            );
        }

        foreach (var crime in crimes)
        {
            crime.Resolution = reportedCrimes.Contains(crime)
                ? CrimeResolution.Reported
                : CrimeResolution.Unreported;
            crime.ResolvedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);

        var hasCrimeWithNoLivingWitnesses = crimes.Any(crime =>
        {
            var crimeWitnesses = witnesses.Where(witness => witness.CrimeId == crime.Id);
            return crimeWitnesses.Any()
                && crimeWitnesses.All(witness => witness.Resolution == CrimeWitnessResolution.Dead);
        });
        if (hasCrimeWithNoLivingWitnesses)
        {
            gameEvents.Enqueue(new CrimeWitnessesRemovedEvent(CrimeKind.Killing));
        }
    }
}
