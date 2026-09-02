using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Events;
using TRPG.Application.Crimes.Events;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.EventHandlers;

internal sealed class CreatureKilledCrimeWitnessEventHandler(
    ICrimesDbContext context,
    IGameClientEventSink gameEvents
) : IDomainEventConsumer<CreatureKilledEvent>
{
    public async Task Handle(
        CreatureKilledEvent domainEvent,
        CancellationToken cancellationToken = default
    )
    {
        var deadWitnesses = await context
            .CrimeWitnesses.Where(witness =>
                witness.WorldId == domainEvent.WorldId
                && witness.CreatureId == domainEvent.CreatureId
                && witness.Resolution == CrimeWitnessResolution.Pending
            )
            .ToArrayAsync(cancellationToken);
        if (deadWitnesses.Length == 0)
        {
            return;
        }

        var crimesWithNoLivingWitnesses = await ResolveAffectedCrimes(
            domainEvent.PlayerId,
            deadWitnesses,
            cancellationToken
        );

        await context.SaveChangesAsync(cancellationToken);

        if (crimesWithNoLivingWitnesses.OfType<TheftCrime>().Any())
        {
            gameEvents.Enqueue(new CrimeWitnessesRemovedEvent(CrimeKind.Theft));
        }

        if (crimesWithNoLivingWitnesses.OfType<KillCrime>().Any())
        {
            gameEvents.Enqueue(new CrimeWitnessesRemovedEvent(CrimeKind.Killing));
        }
    }

    private async Task<Crime[]> ResolveAffectedCrimes(
        Guid playerId,
        IReadOnlyCollection<CrimeWitness> deadWitnesses,
        CancellationToken cancellationToken
    )
    {
        var crimeIds = deadWitnesses.Select(witness => witness.CrimeId).Distinct().ToArray();
        var crimes = await context
            .Crimes.Where(crime =>
                crimeIds.AsEnumerable().Contains(crime.Id)
                && crime.PlayerId == playerId
                && crime.Resolution == CrimeResolution.Pending
            )
            .ToArrayAsync(cancellationToken);
        var witnesses = await context
            .CrimeWitnesses.Where(witness => crimeIds.AsEnumerable().Contains(witness.CrimeId))
            .ToArrayAsync(cancellationToken);

        foreach (var witness in deadWitnesses)
        {
            witness.Resolution = CrimeWitnessResolution.Dead;
            witness.ResolvedAt = DateTime.UtcNow;
        }

        var crimesWithNoLivingWitnesses = crimes
            .Where(crime =>
                witnesses
                    .Where(witness => witness.CrimeId == crime.Id)
                    .All(witness => witness.Resolution == CrimeWitnessResolution.Dead)
            )
            .ToArray();
        foreach (var crime in crimesWithNoLivingWitnesses)
        {
            crime.Resolution = CrimeResolution.Unreported;
            crime.ResolvedAt = DateTime.UtcNow;
        }

        return crimesWithNoLivingWitnesses;
    }
}
