namespace TRPG.Domain.Models;

public class Journey
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid WorldId { get; init; }
    public Guid? TravelCircuitId { get; init; }
    public string? Purpose { get; init; }
    public CreatureActivity? ArrivalActivity { get; init; }
    public Guid? DestinationJobId { get; init; }
    public Guid? DestinationPropId { get; init; }
    public JourneyStatus Status { get; set; }
    public GameInstant PlannedAt { get; init; }
    public GameInstant DepartureAt { get; set; }
    public int CheckpointLegIndex { get; set; }
    public double CheckpointLegProgressMeters { get; set; }
    public GameInstant CheckpointedAt { get; set; }
    public GameInstant? PausedAt { get; set; }
}
