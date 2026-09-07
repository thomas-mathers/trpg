using TRPG.Application.GameTurns.Results;

namespace TRPG.Application.GameTurns;

// An encounter that moves the player leaves the narrator describing a place it was never told
// about, so it furnishes one: an escape route that does not exist, a guard in an empty cell.
internal static class RelocationFacts
{
    public static string Describe(SceneResult scene)
    {
        var place = scene.Room?.Name ?? scene.District?.Name ?? scene.State?.Name ?? "somewhere";
        var building = scene.Building == null ? "" : $" in {scene.Building.Name}";
        var others = scene.NearbyCreatures.Select(creature => creature.Name).ToArray();
        var company =
            others.Length == 0
                ? "The player is alone here."
                : $"The only others here are {string.Join(", ", others)}.";

        return $"""
            The player is now in {place}{building}. {company} Describe nobody else as present, and
            never invent someone arriving to intervene.
            """;
    }
}
