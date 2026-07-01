namespace TRPG.Models;

internal class Location {
    public Guid? BuildingId { get; set; }
    public Guid? RegionId { get; set; }
    public Point Coordinates { get; set; } = null!;
    public Guid? RoomId { get; set; }
}