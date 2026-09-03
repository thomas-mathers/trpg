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
    public required Guid SessionId { get; init; }
    public required Guid LocationId { get; init; }
    public required TimeSpan Delta { get; init; }
}

public enum SleepOutcome
{
    Slept,
    NotYourRoom,
}

internal class SleepInRoomCommandHandler(
    IQueryHandler<GetBedByLocationIdQuery, Bed?> getBedByLocationId,
    ICommandHandler<AdvanceTimeCommand, TimeSpan> advanceTime,
    ICommandHandler<
        ApplyPassiveRegenCommand,
        IReadOnlyDictionary<Guid, Creature>
    > applyPassiveRegen,
    ICommandHandler<SetCreatureRestedUntilCommand> setCreatureRestedUntil
) : ICommandHandler<SleepInRoomCommand, SleepOutcome>
{
    public async Task<SleepOutcome> Handle(
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
            return SleepOutcome.NotYourRoom;
        }

        var playtime = await advanceTime.Handle(
            new AdvanceTimeCommand { SessionId = command.SessionId, Delta = command.Delta },
            cancellationToken
        );

        await applyPassiveRegen.Handle(
            new ApplyPassiveRegenCommand { Playtime = playtime, CreatureIds = [command.PlayerId] },
            cancellationToken
        );

        if (command.Delta >= GameClock.RealTimePerInGameHour)
        {
            await setCreatureRestedUntil.Handle(
                new SetCreatureRestedUntilCommand
                {
                    CreatureId = command.PlayerId,
                    RestedUntilPlaytime = playtime + GameClock.RealTimePerInGameHour * 24,
                },
                cancellationToken
            );
        }

        return SleepOutcome.Slept;
    }
}
