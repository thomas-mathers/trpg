using TRPG.Application.GameSessions;
using TRPG.Contracts.Scenes.Responses;

namespace TRPG.Application.Scenes;

public enum SceneUpdateReason
{
    Moved,
    CatchUp,
    Synced,
}

public record SceneUpdatedEvent(SceneSnapshot Scene, SceneUpdateReason Reason) : GameClientEvent
{
    public override string MethodName => "SceneSnapshot";
    public override object? Payload => Scene;
}
