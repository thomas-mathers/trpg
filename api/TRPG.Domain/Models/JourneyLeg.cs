namespace TRPG.Domain.Models;

public class JourneyLeg
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid JourneyId { get; init; }
    public int Index { get; init; }
    public Guid FromNodeId { get; init; }
    public Guid ToNodeId { get; init; }
    public Guid ConnectorId { get; init; }
    public double Distance { get; init; }
    public required Polyline Path { get; init; }
    public TimeSpan DwellAfter { get; init; }
}
