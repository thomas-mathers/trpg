using TRPG.Application.Common.Commands;
using TRPG.Data;
using TRPG.Domain.Models;

namespace TRPG.Application.Crimes.Commands;

public class AddCrimeWitnessesCommand
{
    public required Guid WorldId { get; init; }
    public required IReadOnlyCollection<Guid> CrimeIds { get; init; }
    public required IReadOnlyCollection<Guid> WitnessCreatureIds { get; init; }
}

internal class AddCrimeWitnessesCommandHandler(TrpgDbContext context)
    : ICommandHandler<AddCrimeWitnessesCommand>
{
    public async Task Handle(
        AddCrimeWitnessesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (command.CrimeIds.Count == 0 || command.WitnessCreatureIds.Count == 0)
        {
            return;
        }

        context.CrimeWitnesses.AddRange(
            command.CrimeIds.SelectMany(crimeId =>
                command.WitnessCreatureIds.Select(witnessId => new CrimeWitness
                {
                    WorldId = command.WorldId,
                    CrimeId = crimeId,
                    CreatureId = witnessId,
                })
            )
        );
        await context.SaveChangesAsync(cancellationToken);
    }
}
