using TRPG.Application.Common.Events;
using TRPG.Application.Common.Queries;
using TRPG.Application.Scenes.Events;
using TRPG.Application.Scenes.Queries;
using TRPG.Application.Scenes.Results;

namespace TRPG.Application.Scenes;

internal sealed class CreatureMovementPublisher(
    IQueryHandler<GetMovementPlaceLabelsQuery, IReadOnlyDictionary<Guid, string>> getPlaceLabels,
    IGameClientEventSink gameEvents
)
{
    public async Task Publish(
        Guid playerId,
        SceneResult previous,
        SceneResult current,
        CancellationToken cancellationToken
    )
    {
        var movements = SceneMovementDetector.Detect(previous, current);
        if (movements.Count == 0)
        {
            return;
        }

        var placeNamesByCreatureId = await getPlaceLabels.Handle(
            new GetMovementPlaceLabelsQuery
            {
                PlayerId = playerId,
                ArrivedCreatureIds = IdsMoving(movements, CreatureMovementDirection.Arrived),
                DepartedCreatureIds = IdsMoving(movements, CreatureMovementDirection.Departed),
            },
            cancellationToken
        );

        var groups = movements.GroupBy(movement => new
        {
            movement.Direction,
            PlaceName = placeNamesByCreatureId.GetValueOrDefault(movement.CreatureId),
        });
        foreach (var group in groups)
        {
            gameEvents.Enqueue(
                new CreaturesMovedEvent(
                    group.Key.Direction,
                    group.Select(movement => movement.Name).ToArray(),
                    group.Key.PlaceName
                )
            );
        }
    }

    private static Guid[] IdsMoving(
        IReadOnlyCollection<SceneMovement> movements,
        CreatureMovementDirection direction
    ) =>
        movements
            .Where(movement => movement.Direction == direction)
            .Select(movement => movement.CreatureId)
            .ToArray();
}
