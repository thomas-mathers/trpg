using System.Text.Json;
using TRPG.Application.Common.Serialization;
using TRPG.Application.Scenes.Mappers;
using TRPG.Application.Scenes.Results;

namespace TRPG.Application.GameTurns;

// An encounter that moves the player leaves the narrator describing a place it was never told
// about, so it furnishes one: an escape route that does not exist, a guard in an empty cell.
internal static class RelocationFacts
{
    public static string Describe(SceneResult scene) =>
        $"The player is now in {Place(scene)}, without having looked or moved to get here.\n{Observation(scene)}";

    // A walk the player made on their own is never narrated, so the model only learns of it here.
    public static string DescribeArrival(SceneResult scene) =>
        $"The player walked to {Place(scene)} on their own, so nothing has been narrated yet.\n{Observation(scene)}";

    private static string Place(SceneResult scene)
    {
        var place = scene.Room?.Name ?? scene.District?.Name ?? scene.State?.Name ?? "somewhere";
        var building = scene.Building == null ? "" : $" in {scene.Building.Name}";
        return $"{place}{building}";
    }

    private static string Observation(SceneResult scene)
    {
        var others = scene.NearbyCreatures.Select(creature => creature.Name).ToArray();
        var company =
            others.Length == 0
                ? "The player is alone here."
                : $"The only others here are {string.Join(", ", others)}.";

        var payload = JsonSerializer.Serialize(scene.ToLlmScene(), TrpgJsonOptions.Default);

        return $"""
            This is what they can observe:
            {payload}
            {company} Describe nobody else as present, and never invent someone arriving to
            intervene. Any name passed to a tool later must be copied verbatim from this scene.
            """;
    }
}
