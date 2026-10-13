using Microsoft.EntityFrameworkCore;
using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Events;
using TRPG.Application.Common.Navigation;
using TRPG.Application.Common.Validation;
using TRPG.Application.Creatures.Events;
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
    public required MovementSpace Space { get; init; }
    public PlayerPose? Previous { get; init; }
}

internal class ApplyPlayerInputCommandHandler(
    ICreaturesDbContext context,
    IGameClientEventSink eventSink
) : ICommandHandler<ApplyPlayerInputCommand, PlayerPose?>
{
    private const double DriftToleranceMeters = 1.5;
    private static readonly TimeSpan MaximumIntegrationGap = TimeSpan.FromSeconds(1);

    public async Task<PlayerPose?> Handle(
        ApplyPlayerInputCommand command,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsFinite(command))
        {
            return null;
        }

        var player = await LoadPlayer(command.PlayerId, cancellationToken);
        if (player is null || !CanWalkIn(player, command.LocationId))
        {
            return null;
        }

        var advance = IntegratePrevious(command, player, command.Space);
        var position = advance.Position;
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

        return new PlayerPose(
            command.LocationId,
            position,
            LatestOf(command.Previous, command.ReceivedAt),
            command.Input,
            advance.Leftover
        );
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

    private static MovementAdvance IntegratePrevious(
        ApplyPlayerInputCommand command,
        WalkState player,
        MovementSpace space
    )
    {
        var previous = command.Previous;
        if (previous?.LocationId != command.LocationId)
        {
            return new MovementAdvance(player.Position, TimeSpan.Zero);
        }

        var elapsed = command.ReceivedAt - previous.ReportedAt;
        if (elapsed <= TimeSpan.Zero)
        {
            return new MovementAdvance(previous.Position, previous.Leftover);
        }

        var gap = elapsed < MaximumIntegrationGap ? elapsed : MaximumIntegrationGap;

        return PlayerMovementIntegrator.AdvanceTurning(
            previous.Position,
            previous.Input,
            command.Input.Heading,
            InLocationPace.MetersPerRealSecond(player.MovementSpeed),
            gap + previous.Leftover,
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
