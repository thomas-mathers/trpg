using TRPG.Application.Common.Events;
using TRPG.Contracts.Scenes.Responses;

namespace TRPG.Application.Scenes;

public enum SceneUpdateReason
{
    Moved,
    CatchUp,
    Synced,
    Reconnected,
}

public record SceneUpdatedEvent(SceneSnapshot Scene, SceneUpdateReason Reason) : GameClientEvent
{
    public override string MethodName => "SceneSnapshot";
    public override object? Payload => Scene;
}
