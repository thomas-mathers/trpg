using TRPG.Application.GameSessions;
using TRPG.Contracts.Scenes.Responses;

namespace TRPG.Application.Scenes;

public enum SceneUpdateReason
{
    Moved,
    CatchUp,
    Synced,
}

public record SceneUpdatedEvent(SceneSnapshot Scene, SceneUpdateReason Reason) : GameTurnEvent
{
    public override string MethodName => "SceneChanged";
    public override object? Payload => Scene;
}
