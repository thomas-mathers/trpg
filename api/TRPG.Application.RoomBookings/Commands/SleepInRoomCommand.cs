using TRPG.Application.Common.Commands;
using TRPG.Application.Common.Queries;
using TRPG.Application.Creatures.Commands;
using TRPG.Application.GameSessions.Commands;
using TRPG.Application.Props.Queries;
using TRPG.Domain;
using TRPG.Domain.Models;

namespace TRPG.Application.RoomBookings.Commands;

public class SleepInRoomCommand
{
    public required Guid PlayerId { get; init; }
    public required Guid WorldId { get; init; }
    public required Guid LocationId { get; init; }
    public required TimeSpan Delta { get; init; }
}

public enum SleepOutcome
{
    Slept,
    NotYourRoom,
}

public record SleepInRoomResult(SleepOutcome Outcome, GameInstant? GameTime = null);

internal class SleepInRoomCommandHandler(
    IQueryHandler<GetBedByLocationIdQuery, Bed?> getBedByLocationId,
    ICommandHandler<AdvanceTimeCommand, GameInstant> advanceTime,
    ICommandHandler<
        ApplyPassiveRegenCommand,
        IReadOnlyDictionary<Guid, Creature>
    > applyPassiveRegen,
    ICommandHandler<SetCreatureRestedUntilCommand> setCreatureRestedUntil
) : ICommandHandler<SleepInRoomCommand, SleepInRoomResult>
{
    public async Task<SleepInRoomResult> Handle(
        SleepInRoomCommand command,
        CancellationToken cancellationToken = default
    )
    {
        var bed = await getBedByLocationId.Handle(
            new GetBedByLocationIdQuery { LocationId = command.LocationId },
            cancellationToken
        );
        if (bed?.AssignedCreatureId != command.PlayerId)
        {
            return new SleepInRoomResult(SleepOutcome.NotYourRoom);
        }

        if (command.Delta <= TimeSpan.Zero || command.Delta > TimeSpan.FromHours(24))
        {
            throw new ArgumentOutOfRangeException(
                nameof(command),
                command.Delta,
                "Sleep duration must be positive and no more than 24 hours."
            );
        }

        var gameTime = await advanceTime.Handle(
            new AdvanceTimeCommand { WorldId = command.WorldId, Delta = command.Delta },
            cancellationToken
        );

        await applyPassiveRegen.Handle(
            new ApplyPassiveRegenCommand { GameTime = gameTime, CreatureIds = [command.PlayerId] },
            cancellationToken
        );

        if (command.Delta >= TimeSpan.FromHours(1))
        {
            await setCreatureRestedUntil.Handle(
                new SetCreatureRestedUntilCommand
                {
                    CreatureId = command.PlayerId,
                    RestedUntilGameTime = gameTime + TimeSpan.FromHours(1) * 24,
                },
                cancellationToken
            );
        }

        return new SleepInRoomResult(SleepOutcome.Slept, gameTime);
    }
}
