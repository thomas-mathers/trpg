using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Navigation;
using TRPG.Application.Common.Queries;
using TRPG.Application.Common.Validation;
using TRPG.Application.Creatures.Events;
using TRPG.Application.Props.Queries;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class ApplyPlayerInputCommand
{
    [NotEmptyGuid]
    public required Guid WorldId { get; init; }

    [NotEmptyGuid]
    public required Guid PlayerId { get; init; }

    [NotEmptyGuid]
    public required Guid LocationId { get; init; }

    public required MovementInput Input { get; init; }
    public required Point ClientPosition { get; init; }
    public required DateTimeOffset ReceivedAt { get; init; }
}

internal class ApplyPlayerInputCommandHandler(
    ICreaturesDbContext context,
    IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>> getLocationsByIds,
    IQueryHandler<GetPropsByLocationIdQuery, IReadOnlyCollection<Prop>> getPropsByLocation,
    IQueryHandler<
        GetBuildingsByLocationQuery,
        IReadOnlyCollection<Building>
    > getBuildingsByLocation,
    IQueryHandler<
        GetPlacedConnectorsByLocationIdQuery,
        IReadOnlyCollection<PlacedConnector>
    > getConnectorsByLocation,
    PlayerPoseStore poseStore,
    IGameClientEventSink eventSink
) : ICommandHandler<ApplyPlayerInputCommand>
{
    private const double DriftToleranceMeters = 0.5;
    private static readonly TimeSpan MaximumIntegrationGap = TimeSpan.FromSeconds(1);

    public async Task Handle(
        ApplyPlayerInputCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsFinite(command))
        {
            return;
        }

        var player = await LoadPlayer(command.PlayerId, cancellationToken);
        if (player is null || !CanWalkIn(player, command.LocationId))
        {
            return;
        }

        var space = await LoadSpace(command.LocationId, cancellationToken);
        if (space is null)
        {
            return;
        }

        var previous = poseStore.Find(command.PlayerId);
        var position = IntegratePrevious(command, player, previous, space);

        poseStore.Set(
            command.PlayerId,
            new PlayerPose(
                command.LocationId,
                position.X,
                position.Y,
                command.Input.Heading,
                LatestOf(previous, command.ReceivedAt),
                IsDirty: true
            )
            {
                Input = command.Input,
            }
        );

        if (Distance(position, command.ClientPosition) > DriftToleranceMeters)
        {
            eventSink.Enqueue(
                new PlayerCorrectedEvent(
                    command.WorldId,
                    command.PlayerId,
                    command.LocationId,
                    position.X,
                    position.Y
                )
            );
        }
    }

    private Task<WalkState?> LoadPlayer(Guid playerId, CancellationToken cancellationToken) =>
        context
            .Creatures.AsNoTracking()
            .Where(creature => creature.Id == playerId)
            .Select(creature => new WalkState(
                creature.LocationId,
                new Point(creature.X, creature.Y),
                creature.MovementSpeed,
                creature.Posture,
                creature.Condition,
                creature.IsEngaged
            ))
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<MovementSpace?> LoadSpace(
        Guid locationId,
        CancellationToken cancellationToken
    )
    {
        var locations = await getLocationsByIds.Handle(
            new GetLocationsByIdsQuery { Ids = [locationId] },
            cancellationToken
        );
        if (!locations.TryGetValue(locationId, out var location))
        {
            return null;
        }

        var props = await getPropsByLocation.Handle(
            new GetPropsByLocationIdQuery { LocationId = locationId },
            cancellationToken
        );
        var buildings = await getBuildingsByLocation.Handle(
            new GetBuildingsByLocationQuery { LocationId = locationId },
            cancellationToken
        );
        var connectors = await getConnectorsByLocation.Handle(
            new GetPlacedConnectorsByLocationIdQuery { LocationId = locationId },
            cancellationToken
        );

        return MovementSpaceBuilder.Build(location, props, buildings, connectors);
    }

    private static Point IntegratePrevious(
        ApplyPlayerInputCommand command,
        WalkState player,
        PlayerPose? previous,
        MovementSpace space
    )
    {
        if (previous?.LocationId != command.LocationId)
        {
            return player.Position;
        }

        var start = new Point(previous.X, previous.Y);
        if (previous.Input is null)
        {
            return start;
        }

        var elapsed = command.ReceivedAt - previous.ReportedAt;
        if (elapsed <= TimeSpan.Zero)
        {
            return start;
        }

        return PlayerMovementIntegrator.Advance(
            start,
            previous.Input,
            InLocationPace.MetersPerRealSecond(player.MovementSpeed),
            elapsed < MaximumIntegrationGap ? elapsed : MaximumIntegrationGap,
            space
        );
    }

    private static DateTimeOffset LatestOf(PlayerPose? previous, DateTimeOffset receivedAt) =>
        previous is not null && previous.ReportedAt > receivedAt ? previous.ReportedAt : receivedAt;

    private static bool IsFinite(ApplyPlayerInputCommand command) =>
        double.IsFinite(command.Input.Forward)
        && double.IsFinite(command.Input.Strafe)
        && double.IsFinite(command.Input.Heading)
        && double.IsFinite(command.ClientPosition.X)
        && double.IsFinite(command.ClientPosition.Y);

    private static bool CanWalkIn(WalkState player, Guid locationId) =>
        player.LocationId == locationId
        && player.Posture == CreaturePosture.Standing
        && player.Condition == CreatureCondition.Awake
        && !player.IsEngaged;

    private static double Distance(Point first, Point second) =>
        Math.Sqrt(Math.Pow(first.X - second.X, 2) + Math.Pow(first.Y - second.Y, 2));

    private sealed record WalkState(
        Guid LocationId,
        Point Position,
        float MovementSpeed,
        CreaturePosture Posture,
        CreatureCondition Condition,
        bool IsEngaged
    );
}
