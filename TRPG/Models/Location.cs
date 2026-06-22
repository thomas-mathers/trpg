namespace TRPG.Models;

internal class Location {
    public Guid? BuildingId { get; set; }
    public Guid? CityId { get; set; }
    public Point Coordinates { get; set; } = null!;
}