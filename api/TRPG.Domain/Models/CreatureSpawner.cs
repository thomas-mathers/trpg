namespace TRPG.Domain.Models;

public class CreatureSpawner
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public Guid LocationId { get; init; }
    public List<CreatureType> ArchetypeCreatureTypes { get; init; } = [];
    public int MaxPopulation { get; init; }
    public int TriggerHour { get; init; }
    public DayOfWeek? SpecificDay { get; init; }
    public TimeSpan LastSyncPlaytime { get; set; }
}
