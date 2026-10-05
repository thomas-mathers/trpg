using TRPG.Domain.Models;

namespace TRPG.Application.WorldGeneration.Generators;

internal readonly record struct GridCell(int X, int Y)
{
    internal GridCell Step(CompassDirection direction) =>
        direction switch
        {
            CompassDirection.North => this with { Y = Y - 1 },
            CompassDirection.East => this with { X = X + 1 },
            CompassDirection.South => this with { Y = Y + 1 },
            CompassDirection.West => this with { X = X - 1 },
            _ => throw new ArgumentOutOfRangeException(nameof(direction)),
        };
}

internal static class DistrictGrid
{
    internal static readonly CompassDirection[] Edges =
    [
        CompassDirection.North,
        CompassDirection.East,
        CompassDirection.South,
        CompassDirection.West,
    ];

    private static readonly GridCell Center = new(0, 0);
    private static readonly GridCell Entrance = new(0, 1);
    private static readonly GridCell BesideEntrance = new(0, 2);

    internal static IReadOnlyDictionary<Guid, GridCell> Assign(IReadOnlyList<District> districts)
    {
        var cells = new Dictionary<Guid, GridCell>();
        var taken = new HashSet<GridCell>();

        foreach (var district in districts.Where(d => d.DistrictType == DistrictType.CityCenter))
        {
            Place(district, Center);
        }

        foreach (var district in districts.Where(d => d.DistrictType == DistrictType.CityEntrance))
        {
            Place(district, Entrance);
        }

        foreach (
            var district in districts.Where(d =>
                d.DistrictType is not (DistrictType.CityCenter or DistrictType.CityEntrance)
            )
        )
        {
            Place(district, NextCell(taken));
        }

        return cells;

        void Place(District district, GridCell cell)
        {
            cells[district.LocationId] = cell;
            taken.Add(cell);
        }
    }

    internal static CompassDirection? DirectionBetween(GridCell from, GridCell to) =>
        Edges
            .Select(edge => (CompassDirection?)edge)
            .FirstOrDefault(edge => from.Step(edge!.Value) == to);

    private static GridCell NextCell(HashSet<GridCell> taken) =>
        taken
            .SelectMany(cell => Edges.Select(cell.Step))
            .Where(cell => !taken.Contains(cell) && cell != BesideEntrance)
            .OrderBy(cell => cell.X * cell.X + cell.Y * cell.Y)
            .ThenBy(cell => cell.Y)
            .ThenBy(cell => cell.X)
            .First();
}
