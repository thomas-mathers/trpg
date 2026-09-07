using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public record CrimeHearsay(Guid CrimeId, Guid CreatureId);

public class RecordCrimeHearsayCommand
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<CrimeHearsay> Entries { get; init; }
}

internal class RecordCrimeHearsayCommandHandler(ICrimesDbContext context)
    : ICommandHandler<RecordCrimeHearsayCommand>
{
    public async Task Handle(
        RecordCrimeHearsayCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.Entries.Count == 0)
        {
            return;
        }

        var crimeIds = command.Entries.Select(entry => entry.CrimeId).Distinct().ToArray();

        var existing = await context
            .CrimeWitnesses.AsNoTracking()
            .Where(witness => crimeIds.AsEnumerable().Contains(witness.CrimeId))
            .Select(witness => new { witness.CrimeId, witness.CreatureId })
            .ToArrayAsync(cancellationToken);

        var alreadyKnown = existing
            .Select(witness => new CrimeHearsay(witness.CrimeId, witness.CreatureId))
            .ToHashSet();

        var added = command
            .Entries.Distinct()
            .Where(entry => !alreadyKnown.Contains(entry))
            .Select(entry => new CrimeWitness
            {
                WorldId = command.WorldId,
                CrimeId = entry.CrimeId,
                CreatureId = entry.CreatureId,
                Kind = CrimeWitnessKind.Heard,
                // Settled on arrival: hearsay only ever follows a crime that was already reported.
                Resolution = CrimeWitnessResolution.Reported,
                ResolvedAt = DateTime.UtcNow,
            })
            .ToArray();

        if (added.Length == 0)
        {
            return;
        }

        context.CrimeWitnesses.AddRange(added);
        await context.SaveChangesAsync(cancellationToken);
    }
}
