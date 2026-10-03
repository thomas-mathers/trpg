using TRPG.Application.Common.Events;
using TRPG.Application.Scenes.Results;

namespace TRPG.Application.Scenes.Events;

public record CreaturesArrivedEvent(Guid WorldId, IReadOnlyCollection<SceneCreatureInfo> Creatures)
    : GameClientEvent(WorldId);
