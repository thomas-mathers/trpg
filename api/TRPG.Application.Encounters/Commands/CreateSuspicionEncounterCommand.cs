using TRPG.Application.Common.Commands;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class CreateSuspicionEncounterCommand
{
    public required Guid WorldId { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid PlayerLocationId { get; init; }
    public required string LocationName { get; init; }
    public required Guid GuardCreatureId { get; init; }
    public required string GuardName { get; init; }
    public required Guid CityFactionId { get; init; }
    public required SuspicionCause Cause { get; init; }
}

internal class CreateSuspicionEncounterCommandHandler(IEncountersDbContext context)
    : ICommandHandler<CreateSuspicionEncounterCommand, SuspicionEncounter>
{
    public async Task<SuspicionEncounter> Handle(
        CreateSuspicionEncounterCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var encounter = new SuspicionEncounter
        {
            WorldId = command.WorldId,
            PlayerId = command.PlayerId,
            LocationId = command.PlayerLocationId,
            LocationName = command.LocationName,
            GuardCreatureId = command.GuardCreatureId,
            GuardName = command.GuardName,
            CityFactionId = command.CityFactionId,
            Cause = command.Cause,
        };
        context.Encounters.Add(encounter);
        await context.SaveChangesAsync(cancellationToken);

        return encounter;
    }
}
