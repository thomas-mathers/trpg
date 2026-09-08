using System.Text;

namespace TRPG.Application.WorldGeneration.Generators;

// Whether a dungeon is connected can be asserted; whether it is worth walking has to be looked at.
internal static class DungeonLayoutDiagram
{
    public static string ToMermaid(DungeonLayout layout)
    {
        var diagram = new StringBuilder("graph TD\n");

        foreach (var room in layout.Rooms)
        {
            diagram.AppendLine($"    {room.Index}[\"{Label(layout, room)}\"]");
        }

        foreach (var passage in layout.Passages)
        {
            diagram.AppendLine($"    {passage.From} --- {passage.To}");
        }

        return diagram.ToString();
    }

    private static string Label(DungeonLayout layout, DungeonRoomNode room)
    {
        var role = room.Index switch
        {
            var index when index == layout.EntranceIndex => "entrance",
            var index when index == layout.BossIndex => "boss",
            _ when room.IsDeadEnd => "dead end",
            _ => "room",
        };

        return $"{room.Index} {role} d{room.DepthFromEntrance}";
    }
}
