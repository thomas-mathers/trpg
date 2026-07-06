using TRPG.Models;

namespace TRPG.Generators;

internal static class JobGenerator {
    private const int SleepStartHour = 22;
    private const int SleepEndHour = 6;
    private const int WorkStartHour = 8;
    private const int WorkEndHour = 20;
    private const int IdleStartHour = 6;
    private const int IdleEndHour = 22;

    public static Job GenerateSleep(Guid stateId, Guid creatureId, Guid roomId, Guid worldId) {
        return new Job {
            StateId = stateId, CreatureId = creatureId, Action = JobAction.Sleep,
            StartHour = SleepStartHour, EndHour = SleepEndHour, Daily = true, Priority = 100, RoomId = roomId,
            WorldId = worldId
        };
    }

    public static Job GenerateIdle(Guid stateId, Guid creatureId, Guid? roomId, Guid worldId) {
        return new Job {
            StateId = stateId, CreatureId = creatureId, Action = JobAction.Idle,
            StartHour = IdleStartHour, EndHour = IdleEndHour, Daily = true, Priority = 0, RoomId = roomId,
            WorldId = worldId
        };
    }

    public static Job GenerateWork(Guid stateId, Guid creatureId, Guid roomId, Guid worldId) {
        return new Job {
            StateId = stateId, CreatureId = creatureId, Action = JobAction.Work,
            StartHour = WorkStartHour, EndHour = WorkEndHour, Daily = true, Priority = 50, RoomId = roomId,
            WorldId = worldId
        };
    }

    public static IReadOnlyList<Job> Generate(
        Guid stateId, Guid creatureId, Guid sleepRoomId, Guid? workRoomId, Guid? idleRoomId, Guid worldId
    ) {
        var jobs = new List<Job> {
            GenerateSleep(stateId, creatureId, sleepRoomId, worldId),
            GenerateIdle(stateId, creatureId, idleRoomId, worldId)
        };

        if (workRoomId != null) {
            jobs.Add(GenerateWork(stateId, creatureId, workRoomId.Value, worldId));
        }

        return jobs;
    }
}
