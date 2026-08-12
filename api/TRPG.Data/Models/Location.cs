namespace TRPG.Data.Models;

public class Location
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public Guid WorldId { get; init; }
    public Guid StateId { get; init; }
    public Guid? CityId { get; init; }
    public Guid? DistrictId { get; init; }
    public Guid? RoomId { get; init; }
}
