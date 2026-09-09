namespace TRPG.Domain.Models;

public class CreatureSpawner
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public Guid LocationId { get; init; }
    public List<CreatureType> ArchetypeCreatureTypes { get; init; } = [];
    public int MaxPopulation { get; init; }

    // A cron expression over in-game time. Anything from "every six hours" to "the first of the
    // month" without needing another column each time a new cadence is wanted.
    public string Schedule { get; init; } = "0 0 * * *";
    public TimeSpan LastSyncPlaytime { get; set; }
}
