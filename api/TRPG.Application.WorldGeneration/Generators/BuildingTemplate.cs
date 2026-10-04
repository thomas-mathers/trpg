using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal record TemplateRoom(string Name, Footprint? Size = null);

internal record TemplateFloor(IReadOnlyList<TemplateRoom> Rooms, Footprint? Hallway = null);

internal record BuildingTemplate(
    string Name,
    Footprint Footprint,
    IReadOnlyList<TemplateFloor> Floors
)
{
    internal bool Covers(IReadOnlyCollection<Room> rooms) =>
        rooms.All(room =>
            room.FloorNumber < Floors.Count
            && Floors[room.FloorNumber].Rooms.Any(templateRoom => templateRoom.Name == room.Name)
        );

    internal Footprint RoomSize(Room room)
    {
        var floor = Floors[room.FloorNumber];

        if (LocationLayoutContext.IsHallway(room))
        {
            return floor.Hallway
                ?? throw new InvalidOperationException(
                    $"Template {Name} has no hallway on floor {room.FloorNumber}."
                );
        }

        return floor.Rooms.Single(templateRoom => templateRoom.Name == room.Name).Size ?? Footprint;
    }
}
