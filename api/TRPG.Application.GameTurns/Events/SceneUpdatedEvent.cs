using TRPG.Application.Common.Events;
using TRPG.Application.Scenes.Queries;

namespace TRPG.Application.GameTurns.Events;

public record SceneUpdatedEvent(SceneResult Scene) : GameClientEvent
{
    public override string MethodName => "SceneSnapshot";
}
