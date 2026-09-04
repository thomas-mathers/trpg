using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class CreateHostileEncounterCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid PlayerLocationId { get; init; }
    public required string? LocationName { get; init; }
    public required Guid FactionId { get; init; }
    public required string FactionName { get; init; }
    public required IReadOnlyCollection<HostileEncounterMemberSnapshot> Members { get; init; }
}

internal class CreateHostileEncounterCommandHandler(IEncountersDbContext context)
    : ICommandHandler<CreateHostileEncounterCommand, HostileEncounter>
{
    public async Task<HostileEncounter> Handle(
        CreateHostileEncounterCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var encounter = new HostileEncounter
        {
            WorldId = command.WorldId,
            PlayerId = command.PlayerId,
            LocationId = command.PlayerLocationId,
            LocationName = command.LocationName,
            FactionId = command.FactionId,
            FactionName = command.FactionName,
            Members = command.Members.ToList(),
        };
        context.Encounters.Add(encounter);
        await context.SaveChangesAsync(cancellationToken);

        return encounter;
    }
}
