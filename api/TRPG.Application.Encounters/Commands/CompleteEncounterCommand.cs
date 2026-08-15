using Microsoft.EntityFrameworkCore;
using TRPG.Data;
using TRPG.Data.Models;

namespace TRPG.Application.Encounters.Commands;

public class CompleteEncounterCommand
{
    public required Guid EncounterId { get; init; }
}

public class CompleteEncounterCommandHandler(TrpgDbContext context)
{
    public Task Handle(
        CompleteEncounterCommand command,
        CancellationToken cancellationToken = default
    ) =>
        context
            .Encounters.Where(e => e.Id == command.EncounterId)
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(e => e.State, EncounterState.Completed)
                        .SetProperty(e => e.CompletedAt, DateTime.UtcNow),
                cancellationToken
            );
}
