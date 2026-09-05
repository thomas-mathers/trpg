using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Domain.Models;

namespace TRPG.Application.Encounters.Commands;

public class EvaluateOverdueRoomKeyEncounterCommand
{
    public required Guid WorldId { get; init; }
    public required TimeSpan Playtime { get; init; }
    public required Guid PlayerId { get; init; }
    public required Guid FromLocationId { get; init; }
    public required Guid ToLocationId { get; init; }
}

internal class EvaluateOverdueRoomKeyEncounterCommandHandler(
    IQueryHandler<GetBuildingByLocationIdQuery, BuildingIdentity?> getBuildingByLocationId,
    ICommandHandler<
        ConfrontOverdueRoomKeyCommand,
        ConfrontOverdueRoomKeyResult
    > confrontOverdueRoomKey
) : ICommandHandler<EvaluateOverdueRoomKeyEncounterCommand, ConfrontOverdueRoomKeyResult>
{
    public async Task<ConfrontOverdueRoomKeyResult> Handle(
        EvaluateOverdueRoomKeyEncounterCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var innBuilding = await ResolveCrossedInn(command, cancellationToken);
        if (innBuilding == null)
        {
            return new ConfrontOverdueRoomKeyResult(null);
        }

        return await confrontOverdueRoomKey.Handle(
            new ConfrontOverdueRoomKeyCommand
            {
                WorldId = command.WorldId,
                Playtime = command.Playtime,
                PlayerId = command.PlayerId,
                LocationId = command.ToLocationId,
                BuildingId = innBuilding.Id,
            },
            cancellationToken
        );
    }

    private async Task<BuildingIdentity?> ResolveCrossedInn(
        EvaluateOverdueRoomKeyEncounterCommand command,
        CancellationToken cancellationToken
    )
    {
        var fromBuilding = await getBuildingByLocationId.Handle(
            new GetBuildingByLocationIdQuery { LocationId = command.FromLocationId },
            cancellationToken
        );
        var toBuilding = await getBuildingByLocationId.Handle(
            new GetBuildingByLocationIdQuery { LocationId = command.ToLocationId },
            cancellationToken
        );

        if (fromBuilding?.Id == toBuilding?.Id)
        {
            return null;
        }

        // Either direction counts — a player who left before it was due must still get caught coming back in.
        return fromBuilding is { BuildingType: BuildingType.Inn } ? fromBuilding
            : toBuilding is { BuildingType: BuildingType.Inn } ? toBuilding
            : null;
    }
}
