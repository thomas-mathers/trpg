using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class CreateShakedownEncounterCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid PlayerLocationId { get; init; }
    public required string? LocationName { get; init; }
    public required Guid FactionId { get; init; }
    public required string FactionName { get; init; }
    public required int TollAmount { get; init; }
    public required IReadOnlyCollection<HostileEncounterMemberSnapshot> Members { get; init; }
}

internal class CreateShakedownEncounterCommandHandler(IEncountersDbContext context)
    : ICommandHandler<CreateShakedownEncounterCommand, ShakedownEncounter>
{
    public async Task<ShakedownEncounter> Handle(
        CreateShakedownEncounterCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var encounter = new ShakedownEncounter
        {
            WorldId = command.WorldId,
            PlayerId = command.PlayerId,
            LocationId = command.PlayerLocationId,
            LocationName = command.LocationName,
            FactionId = command.FactionId,
            FactionName = command.FactionName,
            TollAmount = command.TollAmount,
            Members = command.Members.ToList(),
        };
        context.Encounters.Add(encounter);
        await context.SaveChangesAsync(cancellationToken);

        return encounter;
    }
}
