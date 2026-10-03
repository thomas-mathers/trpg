using TRPG.Application.Common.Events;
using TRPG.Application.Scenes.Results;

namespace TRPG.Application.Scenes.Events;

public record CaravansArrivedEvent(Guid WorldId, IReadOnlyCollection<SceneCaravanInfo> Caravans)
    : GameClientEvent(WorldId);
