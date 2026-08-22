namespace TRPG.Domain.Models;

public enum LocationKind
{
    District,
    Room,
    Wilderness,
}

public class Location
{
    public Guid CoarseAnchorLocationId { get; init; }
    public string Description { get; init; } = "";
    public Guid Id { get; init; } = Guid.NewGuid();
    public LocationKind Kind { get; init; }
    public string Name { get; init; } = "";
    public Guid WorldId { get; init; }
    public Guid StateId { get; init; }
    public Guid? CityId { get; init; }
    public Guid? DistrictId { get; init; }
    public Guid? RoomId { get; init; }
}
