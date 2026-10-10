namespace TRPG.Domain.Models;

public class TravelCircuitLeg
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid TravelCircuitId { get; init; }
    public int Index { get; init; }
    public Guid FromNodeId { get; init; }
    public Guid ToNodeId { get; init; }
    public Guid ConnectorId { get; init; }
    public TimeSpan DwellAfter { get; init; }
}
