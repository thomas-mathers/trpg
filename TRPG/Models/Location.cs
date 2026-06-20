namespace TRPG.Models;

public class Location
{
    public Point Coordinates { get; set; } = null!;
    public Guid WorldId { get; set; }
    public Guid? CityId { get; set; }
    public Guid? BuildingId { get; set; }
}
