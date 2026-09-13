namespace TRPG.Domain.Models;

public class QuestSeedSchedule
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public Guid LocationId { get; init; }

    // A cron expression over in-game time, same idiom as CreatureSpawner — this location gets a
    // chance to seed a rescue quest each time the schedule comes due, not on every single visit.
    public string Schedule { get; init; } = "0 0 * * *";
    public TimeSpan LastSyncPlaytime { get; set; }
}
