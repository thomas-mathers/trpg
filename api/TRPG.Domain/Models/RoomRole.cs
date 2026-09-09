namespace TRPG.Domain.Models;

// What a room is for. Durable, like a building's type: it decides what the room is called, what is
// put in it, and how it is drawn.
public enum RoomRole
{
    Entrance,
    BossChamber,
    Passage,
    GuardPost,
    Storeroom,
    TreasureRoom,
    Shrine,
    Study,
    CellBlock,
    CollapsedGallery,
    FloodedSump,
}
