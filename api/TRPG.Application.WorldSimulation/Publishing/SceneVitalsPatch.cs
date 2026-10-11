using TRPG.Application.Creatures.Results;
using TRPG.Application.Scenes.Results;

namespace TRPG.Application.WorldSimulation.Publishing;

internal static class SceneVitalsPatch
{
    public static SceneResult Apply(SceneResult scene, IEnumerable<CreatureVitals> vitals)
    {
        var vitalsById = vitals.ToDictionary(entry => entry.CreatureId);

        return scene with
        {
            Player = Patch(scene.Player, vitalsById),
            NearbyCreatures =
            [
                .. scene.NearbyCreatures.Select(creature => Patch(creature, vitalsById)),
            ],
        };
    }

    private static SceneCreatureInfo Patch(
        SceneCreatureInfo creature,
        IReadOnlyDictionary<Guid, CreatureVitals> vitalsById
    ) =>
        vitalsById.TryGetValue(creature.Id, out var vitals)
            ? creature with
            {
                CurrentHp = vitals.CurrentHp,
                MaximumHp = vitals.MaximumHp,
                CurrentAp = vitals.CurrentAp,
                MaximumAp = vitals.MaximumAp,
                CurrentMp = vitals.CurrentMp,
                MaximumMp = vitals.MaximumMp,
            }
            : creature;
}
