using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class CompleteEncounterCommand
{
    public required Guid EncounterId { get; init; }
}

internal class CompleteEncounterCommandHandler(IEncountersDbContext context)
    : ICommandHandler<CompleteEncounterCommand>
{
    public async Task Handle(
        CompleteEncounterCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var rowsAffected = await context
            .Encounters.Where(e => e.Id == command.EncounterId && e.State == EncounterState.Active)
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(e => e.State, EncounterState.Completed)
                        .SetProperty(e => e.CompletedAt, DateTime.UtcNow),
                cancellationToken
            );

        if (rowsAffected != 1)
        {
            throw new InvalidOperationException(
                $"Encounter {command.EncounterId} is not active and cannot be resolved."
            );
        }
    }
}
