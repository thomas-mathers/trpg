using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Common.Validation;
using TRPG.Application.Worlds.Queries;
using TRPG.Data.ModuleContexts;
using TRPG.Domain.Models;

namespace TRPG.Application.Creatures.Commands;

public class ReportPlayerPoseCommand
{
    [NotEmptyGuid]
    public required Guid PlayerId { get; init; }

    [NotEmptyGuid]
    public required Guid LocationId { get; init; }

    public required double X { get; init; }
    public required double Y { get; init; }
    public required double Angle { get; init; }
}

internal class ReportPlayerPoseCommandHandler(
    ICreaturesDbContext context,
    IQueryHandler<GetLocationsByIdsQuery, IReadOnlyDictionary<Guid, Location>> getLocationsByIds,
    PlayerPoseStore poseStore,
    TimeProvider timeProvider
) : ICommandHandler<ReportPlayerPoseCommand>
{
    private const double WalkMetersPerSecondAtBaseSpeed = 3;
    private const double BaseMovementSpeed = 50;
    private const double SpeedTolerance = 1.5;
    private const double BurstAllowanceMeters = 1;

    public async Task Handle(
        ReportPlayerPoseCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var player = await context
            .Creatures.AsNoTracking()
            .Where(creature => creature.Id == command.PlayerId)
            .Select(creature => new WalkState(
                creature.LocationId,
                creature.MovementSpeed,
                creature.Posture,
                creature.Condition,
                creature.IsEngaged
            ))
            .FirstOrDefaultAsync(cancellationToken);
        if (player is null || !CanWalkIn(player, command.LocationId))
        {
            return;
        }

        var locations = await getLocationsByIds.Handle(
            new GetLocationsByIdsQuery { Ids = [command.LocationId] },
            cancellationToken
        );
        if (!locations.TryGetValue(command.LocationId, out var location))
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var target = new PlayerPose(
            command.LocationId,
            Math.Clamp(command.X, 0, location.Width),
            Math.Clamp(command.Y, 0, location.Depth),
            command.Angle,
            now,
            IsDirty: true
        );
        var previous = poseStore.Find(command.PlayerId);
        if (previous?.LocationId == command.LocationId && !IsReachable(previous, target, player))
        {
            return;
        }

        poseStore.Set(command.PlayerId, target);
    }

    private static bool CanWalkIn(WalkState player, Guid locationId) =>
        player.LocationId == locationId
        && player.Posture == CreaturePosture.Standing
        && player.Condition == CreatureCondition.Awake
        && !player.IsEngaged;

    private static bool IsReachable(PlayerPose previous, PlayerPose target, WalkState player)
    {
        var elapsedSeconds = (target.ReportedAt - previous.ReportedAt).TotalSeconds;
        var metersPerSecond =
            WalkMetersPerSecondAtBaseSpeed * player.MovementSpeed / BaseMovementSpeed;
        var allowedMeters =
            (metersPerSecond * elapsedSeconds * SpeedTolerance) + BurstAllowanceMeters;
        var distance = Math.Sqrt(
            Math.Pow(target.X - previous.X, 2) + Math.Pow(target.Y - previous.Y, 2)
        );

        return distance <= allowedMeters;
    }

    private sealed record WalkState(
        Guid LocationId,
        float MovementSpeed,
        CreaturePosture Posture,
        CreatureCondition Condition,
        bool IsEngaged
    );
}
