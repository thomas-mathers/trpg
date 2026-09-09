using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class CreateTrapEncounterCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid PlayerLocationId { get; init; }
    public string? LocationName { get; init; }
    public required Guid TriggerId { get; init; }
    public required TrapKind TrapKind { get; init; }
    public required Guid TargetLocationId { get; init; }
    public required string TargetLocationName { get; init; }
}

internal class CreateTrapEncounterCommandHandler(IEncountersDbContext context)
    : ICommandHandler<CreateTrapEncounterCommand, TrapEncounter>
{
    public async Task<TrapEncounter> Handle(
        CreateTrapEncounterCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var encounter = new TrapEncounter
        {
            WorldId = command.WorldId,
            PlayerId = command.PlayerId,
            LocationId = command.PlayerLocationId,
            LocationName = command.LocationName,
            TriggerId = command.TriggerId,
            TrapKind = command.TrapKind,
            TargetLocationId = command.TargetLocationId,
            TargetLocationName = command.TargetLocationName,
        };
        context.Encounters.Add(encounter);
        await context.SaveChangesAsync(cancellationToken);

        return encounter;
    }
}
