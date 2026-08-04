namespace TRPG.Data.Models;

public class LocationConnectorKey
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ItemId { get; init; }
    public Guid LocationConnectorId { get; init; }
    public Guid WorldId { get; init; }
}
